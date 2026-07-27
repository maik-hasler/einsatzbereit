using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Users.AdminRestoreUser.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.AdminRestoreUser.v1;

internal sealed class AdminRestoreUserEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPut("/admin/users/{userId:guid}/restore", AdminRestoreUserAsync)
			.WithName("AdminRestoreUser")
			.WithTags("Admin")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitAdminPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> AdminRestoreUserAsync(
		[FromRoute] Guid userId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		await sender.Send(new AdminRestoreUserCommand(userId), cancellationToken);

		return Results.NoContent();
	}
}
