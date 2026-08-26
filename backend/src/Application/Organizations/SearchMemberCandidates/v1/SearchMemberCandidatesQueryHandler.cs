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

		var match = await keycloak.FindUserByExactMatchAsync(query.Search, cancellationToken);
		if (match is null)
			return [];

		var currentMembers = await keycloak.GetMembersAsync(query.OrganizationId, cancellationToken);
		if (currentMembers.Any(m => m.UserId == match.UserId))
			return [ToDto(match, MemberCandidateStatus.AlreadyMember)];

		var invitations = await dbContext.GetInvitationsForOrganizationAsync(
			OrganizationId.Create(query.OrganizationId).GetValueOrThrow(),
			cancellationToken);

		var hasPendingInvitation = invitations.Any(i =>
			i.Status == InvitationStatus.Pending && i.InviteeId.Value == match.UserId);

		var status = hasPendingInvitation
			? MemberCandidateStatus.AlreadyInvited
			: MemberCandidateStatus.Available;

		return [ToDto(match, status)];
	}

	private static MemberCandidateDto ToDto(KeycloakOrganizationMember member, MemberCandidateStatus status) =>
		new(member.UserId, member.Username, member.FirstName, member.LastName, status.ToString());
}
