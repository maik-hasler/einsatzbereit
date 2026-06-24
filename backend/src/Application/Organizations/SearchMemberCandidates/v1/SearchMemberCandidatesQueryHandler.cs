using Application.Common.Keycloak;
using Application.Common.Messaging;

namespace Application.Organizations.SearchMemberCandidates.v1;

internal sealed class SearchMemberCandidatesQueryHandler(
	IKeycloakOrganizationService keycloak)
	: IQueryHandler<SearchMemberCandidatesQuery, IReadOnlyList<MemberCandidateDto>>
{
	public async ValueTask<IReadOnlyList<MemberCandidateDto>> Handle(
		SearchMemberCandidatesQuery query,
		CancellationToken cancellationToken)
	{
		var allResults = await keycloak.SearchUsersAsync(
			query.Search,
			cancellationToken: cancellationToken);

		var currentMembers = await keycloak.GetMembersAsync(
			query.OrganizationId,
			cancellationToken);

		var memberIds = currentMembers.Select(m => m.UserId).ToHashSet();

		return allResults
			.Where(u => !memberIds.Contains(u.UserId))
			.Select(u => new MemberCandidateDto(
				u.UserId,
				u.Username,
				u.FirstName,
				u.LastName,
				u.Email))
			.ToList();
	}
}
