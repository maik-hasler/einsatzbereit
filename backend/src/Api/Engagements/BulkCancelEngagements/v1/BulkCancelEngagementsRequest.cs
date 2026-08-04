using System.ComponentModel.DataAnnotations;

namespace Api.Engagements.BulkCancelEngagements.v1;

public sealed record BulkCancelEngagementsRequest(
	IReadOnlyList<Guid> EngagementIds,
	[MaxLength(500)] string? Reason = null);
