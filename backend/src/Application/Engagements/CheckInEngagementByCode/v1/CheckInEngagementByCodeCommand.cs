using Application.Common.Messaging;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.CheckInEngagementByCode.v1;

public sealed record CheckInEngagementByCodeCommand(
	VolunteerOpportunityId OpportunityId,
	string Code,
	UserId RequestingUserId)
	: ICommand<Engagement>;
