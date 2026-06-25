using Application.Common.Messaging;
using Domain.Organizations;

namespace Application.Organizations.GetOrgInvitations.v1;

public sealed record GetOrgInvitationsQuery(OrganizationId OrganizationId) : IQuery<List<OrgInvitationDto>>;
