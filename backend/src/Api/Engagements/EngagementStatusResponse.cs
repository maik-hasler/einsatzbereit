namespace Api.Engagements;

public sealed record EngagementStatusResponse(
    Guid Id,
    string Status,
    DateTimeOffset? ModifiedOn);
