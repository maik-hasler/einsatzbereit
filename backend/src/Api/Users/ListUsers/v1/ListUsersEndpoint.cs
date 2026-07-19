using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Users.ListUsers.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.ListUsers.v1;

internal sealed class ListUsersEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/admin/users", ListUsersAsync)
			.WithName("ListUsers")
			.WithTags("Admin")
			.Produces<IReadOnlyList<AdminUserListItem>>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitAdminPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> ListUsersAsync(
		[FromQuery] string? search,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var result = await sender.Send(new ListUsersQuery(search), cancellationToken);

		return Results.Ok(result);
	}
}
