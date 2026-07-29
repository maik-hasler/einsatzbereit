using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Domain.Engagements;

public sealed record EngagementReminderDueDomainEvent(
	EngagementId EngagementId,
	UserId VolunteerId,
	VolunteerOpportunityId OpportunityId,
	TimeSlotId TimeSlotId)
	: DomainEvent;
