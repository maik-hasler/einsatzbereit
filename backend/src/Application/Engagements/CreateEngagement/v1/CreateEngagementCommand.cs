using Application.Common.Messaging;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.CreateEngagement.v1;

public sealed record CreateEngagementCommand(
    VolunteerOpportunityId OpportunityId,
    UserId VolunteerId,
    TimeSlotId? TimeSlotId,
    string? Message)
    : ICommand<Engagement>;
