using Application.Common.Messaging;
using Domain.Organizations;
using Domain.Users;

namespace Application.Organizations.GetOrgInvitations.v1;

public sealed record GetOrgInvitationsQuery(
	OrganizationId OrganizationId,
	UserId RequestingUserId) : IQuery<List<OrgInvitationDto>>;
