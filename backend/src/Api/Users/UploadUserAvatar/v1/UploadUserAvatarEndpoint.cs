using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Users.UploadUserAvatar.v1;
using Domain.Primitives;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Users.UploadUserAvatar.v1;

internal sealed class UploadUserAvatarEndpoint
	: IEndpoint
{
	private const long MaxFileSizeBytes = 2 * 1024 * 1024;

	private static readonly string[] AllowedContentTypes =
		["image/jpeg", "image/png", "image/webp"];

	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPut("/users/me/avatar", UploadUserAvatarAsync)
			.WithName("UploadUserAvatar")
			.WithTags("Users")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.DisableAntiforgery()
			.MapToApiVersion(1);

	private static async Task<IResult> UploadUserAvatarAsync(
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
				"Avatar image must not be empty.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (file.Length > MaxFileSizeBytes)
		{
			return Results.Problem(
				"Avatar image must not exceed 2 MB.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
		{
			return Results.Problem(
				"Avatar image must be a JPEG, PNG or WebP image.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		using var memoryStream = new MemoryStream();
		await file.CopyToAsync(memoryStream, cancellationToken);

		await sender.Send(
			new UploadUserAvatarCommand(
				userId,
				memoryStream.ToArray(),
				file.ContentType),
			cancellationToken);

		return Results.NoContent();
	}
}
