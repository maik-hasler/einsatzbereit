using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.UploadOpportunityBanner.v1;
using Domain.Primitives;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.VolunteerOpportunities.UploadOpportunityBanner.v1;

internal sealed class UploadOpportunityBannerEndpoint
	: IEndpoint
{
	private const long MaxFileSizeBytes = 2 * 1024 * 1024;

	private static readonly string[] AllowedContentTypes =
		["image/jpeg", "image/png", "image/webp"];

	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPut("/volunteer-opportunities/{opportunityId:guid}/banner", UploadOpportunityBannerAsync)
			.WithName("UploadOpportunityBanner")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.DisableAntiforgery()
			.MapToApiVersion(1);

	private static async Task<IResult> UploadOpportunityBannerAsync(
		[FromRoute] Guid opportunityId,
		IFormFile file,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid)
			? new UserId(uid)
			: throw new DomainException("Invalid user.");

		if (file.Length == 0)
		{
			return Results.Problem(
				"Banner image must not be empty.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (file.Length > MaxFileSizeBytes)
		{
			return Results.Problem(
				"Banner image must not exceed 2 MB.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
		{
			return Results.Problem(
				"Banner image must be a JPEG, PNG or WebP image.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		using var memoryStream = new MemoryStream();
		await file.CopyToAsync(memoryStream, cancellationToken);

		await sender.Send(
			new UploadOpportunityBannerCommand(
				opportunityId,
				memoryStream.ToArray(),
				file.ContentType,
				userId),
			cancellationToken);

		return Results.NoContent();
	}
}
