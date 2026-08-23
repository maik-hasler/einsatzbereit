using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;

namespace Application.Organizations.SearchMemberCandidates.v1;

internal sealed class SearchMemberCandidatesQueryHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloak)
	: IQueryHandler<SearchMemberCandidatesQuery, IReadOnlyList<MemberCandidateDto>>
{
	public async ValueTask<IReadOnlyList<MemberCandidateDto>> Handle(
		SearchMemberCandidatesQuery query,
		CancellationToken cancellationToken)
	{
		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			query.OrganizationId,
			query.RequestingUserId,
			cancellationToken);

		var allResults = await keycloak.SearchUsersAsync(
			query.Search,
			cancellationToken: cancellationToken);

		var currentMembers = await keycloak.GetMembersAsync(
			query.OrganizationId,
			cancellationToken);

		var memberIds = currentMembers.Select(m => m.UserId).ToHashSet();

		var invitations = await dbContext.GetInvitationsForOrganizationAsync(
			OrganizationId.Create(query.OrganizationId).GetValueOrThrow(),
			cancellationToken);

		var pendingInviteeIds = invitations
			.Where(i => i.Status == InvitationStatus.Pending)
			.Select(i => i.InviteeId.Value)
			.ToHashSet();

		return allResults
			.Where(u => !memberIds.Contains(u.UserId) && !pendingInviteeIds.Contains(u.UserId))
			.Select(u => new MemberCandidateDto(
				u.UserId,
				u.Username,
				u.FirstName,
				u.LastName))
			.ToList();
	}
}
