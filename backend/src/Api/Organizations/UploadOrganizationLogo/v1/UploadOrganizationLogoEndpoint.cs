using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Organizations.UploadOrganizationLogo.v1;
using Domain.Primitives;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Organizations.UploadOrganizationLogo.v1;

internal sealed class UploadOrganizationLogoEndpoint
	: IEndpoint
{
	private const long MaxFileSizeBytes = 2 * 1024 * 1024;

	private static readonly string[] AllowedContentTypes =
		["image/jpeg", "image/png", "image/webp"];

	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPut("/organizations/{organizationId:guid}/logo", UploadOrganizationLogoAsync)
			.WithName("UploadOrganizationLogo")
			.WithTags("Organizations")
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

	private static async Task<IResult> UploadOrganizationLogoAsync(
		[FromRoute] Guid organizationId,
		IFormFile file,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid)
			? UserId.Create(uid).GetValueOrThrow()
			: throw new ResultFailureException(Error.Validation("User.InvalidId", "Invalid user."));

		if (file.Length == 0)
		{
			return Results.Problem(
				"Logo image must not be empty.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (file.Length > MaxFileSizeBytes)
		{
			return Results.Problem(
				"Logo image must not exceed 2 MB.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
		{
			return Results.Problem(
				"Logo image must be a JPEG, PNG or WebP image.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		using var memoryStream = new MemoryStream();
		await file.CopyToAsync(memoryStream, cancellationToken);

		await sender.Send(
			new UploadOrganizationLogoCommand(
				organizationId,
				memoryStream.ToArray(),
				file.ContentType,
				userId),
			cancellationToken);

		return Results.NoContent();
	}
}
