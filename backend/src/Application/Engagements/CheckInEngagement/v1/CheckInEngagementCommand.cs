using Application.Common.Messaging;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.CheckInEngagement.v1;

public sealed record CheckInEngagementCommand(
	VolunteerOpportunityId OpportunityId,
	EngagementId EngagementId,
	UserId RequestingUserId)
	: ICommand<Engagement>;
