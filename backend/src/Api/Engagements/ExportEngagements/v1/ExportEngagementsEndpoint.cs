using System.Security.Claims;
using System.Text;
using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Engagements.ExportEngagements.v1;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;

namespace Api.Engagements.ExportEngagements.v1;

internal sealed class ExportEngagementsEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/volunteer-opportunities/{opportunityId:guid}/engagements/export", ExportEngagementsAsync)
			.WithName("ExportEngagements")
			.WithTags("Engagements")
			.Produces(StatusCodes.Status200OK, contentType: "text/csv")
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> ExportEngagementsAsync(
		[FromRoute] Guid opportunityId,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid)
			? UserId.Create(uid).GetValueOrThrow()
			: throw new ResultFailureException(Error.Validation("User.InvalidId", "Invalid user."));

		var file = await sender.Send(
			new ExportEngagementsQuery(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), userId),
			cancellationToken);

		// A UTF-8 BOM (#1675) - without it, Excel on a German Windows install
		// guesses the ANSI codepage instead of UTF-8 and mangles every umlaut
		// ("Müller" -> "MÃ¼ller") in a volunteer's name.
		byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(file.Content)];

		return Results.File(
			bytes,
			contentType: "text/csv; charset=utf-8",
			fileDownloadName: file.FileName);
	}
}
