namespace Api.Engagements.BulkConfirmEngagements.v1;

public sealed record BulkConfirmEngagementsRequest(IReadOnlyList<Guid> EngagementIds);
