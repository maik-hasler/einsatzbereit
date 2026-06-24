using Application.Common.Messaging;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.GetOpportunityCheckInPin.v1;

public sealed record GetOpportunityCheckInPinQuery(
	VolunteerOpportunityId OpportunityId,
	UserId RequestingUserId)
	: IQuery<string?>;
