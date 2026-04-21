namespace Api.Engagements.CreateEngagement.v1;

public sealed record CreateEngagementRequest(
    string Type,
    Guid? TimeSlotId,
    string? Message);
