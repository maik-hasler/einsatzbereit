namespace Api.Engagements.CreateEngagement.v1;

public sealed record CreateEngagementResponse(
    Guid Id,
    Guid OpportunityId,
    string Status,
    DateTimeOffset CreatedOn);
