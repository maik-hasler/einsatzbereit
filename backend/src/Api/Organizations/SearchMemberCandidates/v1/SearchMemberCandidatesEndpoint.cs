using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Organizations.SearchMemberCandidates.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Organizations.SearchMemberCandidates.v1;

internal sealed class SearchMemberCandidatesEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/organizations/{organizationId:guid}/members/search", SearchAsync)
			.WithName("SearchMemberCandidates")
			.Produces<IReadOnlyList<MemberCandidateDto>>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.MapToApiVersion(1);
	}

	private static async Task<IResult> SearchAsync(
		[FromRoute] Guid organizationId,
		[FromQuery] string q,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
			return Results.Ok(Array.Empty<MemberCandidateDto>());

		var result = await sender.Send(
			new SearchMemberCandidatesQuery(organizationId, q),
			cancellationToken);

		return Results.Ok(result);
	}
}
