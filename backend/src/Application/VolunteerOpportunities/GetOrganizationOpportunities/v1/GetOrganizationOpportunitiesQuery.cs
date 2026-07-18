using Application.Common.Messaging;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Domain.Users;

namespace Application.VolunteerOpportunities.GetOrganizationOpportunities.v1;

public sealed record GetOrganizationOpportunitiesQuery(
	Guid OrganizationId,
	UserId RequestingUserId)
	: IQuery<IReadOnlyList<VolunteerOpportunitySummary>>;
