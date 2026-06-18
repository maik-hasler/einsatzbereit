using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Organizations.UpdateOrganization.v1;
using Domain.Primitives;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Organizations.UpdateOrganization.v1;

internal sealed class UpdateOrganizationEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPut("/organizations/{organizationId:guid}", UpdateOrganizationAsync)
			.WithName("UpdateOrganization")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);
	}

	private static async Task<IResult> UpdateOrganizationAsync(
		[FromRoute] Guid organizationId,
		[FromBody] UpdateOrganizationRequest request,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid) ? new UserId(uid) : throw new DomainException("Invalid user.");

		if (request.Name.Length > 100)
			return Results.Problem("Name must not exceed 100 characters.", statusCode: StatusCodes.Status400BadRequest);

		if (request.Description?.Length > 1000)
			return Results.Problem("Description must not exceed 1000 characters.", statusCode: StatusCodes.Status400BadRequest);

		var addressCommand = request.Address is null
			? null
			: new UpdateAddressCommand(
				request.Address.Street,
				request.Address.HouseNumber,
				request.Address.ZipCode,
				request.Address.City);

		var command = new UpdateOrganizationCommand(
			organizationId,
			request.Name,
			request.Description,
			request.ContactEmail,
			request.ContactPhone,
			request.Website,
			addressCommand,
			userId);

		await sender.Send(command, cancellationToken);

		return Results.NoContent();
	}
}
