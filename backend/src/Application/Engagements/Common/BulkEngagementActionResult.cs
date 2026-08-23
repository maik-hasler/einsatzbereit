namespace Application.Engagements.Common;

public sealed record BulkEngagementActionResult(
	IReadOnlyList<BulkEngagementActionSuccess> Succeeded,
	IReadOnlyList<BulkEngagementActionFailure> Failed);

public sealed record BulkEngagementActionSuccess(
	Guid EngagementId,
	string Status,
	string? CancellationReason = null);

public sealed record BulkEngagementActionFailure(
	Guid EngagementId,
	string ErrorCode,
	string Message);
