namespace Application.Engagements.Common;

// Best-effort/partial-success result for a bulk engagement action: each id is
// processed independently (via a nested ISender.Send of the existing single-item
// command), so one invalid id (wrong status, anonymized volunteer, foreign org)
// doesn't block the rest of the batch - see BulkConfirmEngagementsCommandHandler/
// BulkCancelEngagementsCommandHandler.
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
