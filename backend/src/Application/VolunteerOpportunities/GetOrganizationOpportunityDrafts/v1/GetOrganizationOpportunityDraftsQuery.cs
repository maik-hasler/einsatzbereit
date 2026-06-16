using Application.Common.Messaging;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Domain.Users;

namespace Application.VolunteerOpportunities.GetOrganizationOpportunityDrafts.v1;

public sealed record GetOrganizationOpportunityDraftsQuery(
	Guid OrganizationId,
	UserId RequestingUserId)
	: IQuery<IReadOnlyList<VolunteerOpportunitySummary>>;
