using Domain.Primitives;
using Domain.Users;

namespace Domain.Reports;

public sealed class Report
	: AggregateRoot<ReportId>,
		IAuditableEntity
{
	public const int MaxDetailsLength = 1000;

	public ReportTargetType TargetType { get; private set; }

	public Guid TargetId { get; private set; }

	public UserId ReporterId { get; private set; }

	public ReportReason Reason { get; private set; }

	public string? Details { get; private set; }

	public ReportStatus Status { get; private set; }

	public UserId? ResolvedByUserId { get; private set; }

	public DateTimeOffset? ResolvedOn { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

	// Reports targeting a user survive that user's own account deletion as
	// moderation history (DeleteMyAccountCommandHandler only hard-deletes
	// reports the deleted user *filed*, via DeleteReportsForReporterAsync) -
	// but they still need their own retention limit once the account they
	// concern is gone for good. Stamped once, when the target account is
	// deleted, so AbuseReportRetentionJob can prune from that point rather
	// than from CreatedOn (#1725).
	public DateTimeOffset? TargetDeletedOn { get; private set; }

#pragma warning disable CS8618
	private Report() : base(default) { }
#pragma warning restore CS8618

	private Report(
		ReportId id,
		ReportTargetType targetType,
		Guid targetId,
		UserId reporterId,
		ReportReason reason,
		string? details)
		: base(id)
	{
		TargetType = targetType;
		TargetId = targetId;
		ReporterId = reporterId;
		Reason = reason;
		Details = details;
		Status = ReportStatus.Open;
	}

	public static Result<Report> Create(
		ReportTargetType targetType,
		Guid targetId,
		UserId reporterId,
		ReportReason reason,
		string? details)
	{
		if (details is { Length: > MaxDetailsLength })
			return Result.Failure<Report>(Error.Validation("Report.DetailsTooLong", $"Details must not exceed {MaxDetailsLength} characters."));

		return new Report(ReportId.New(), targetType, targetId, reporterId, reason, details);
	}

	public Result Dismiss(UserId resolvedByUserId, DateTimeOffset resolvedOn)
	{
		if (Status != ReportStatus.Open)
			return Result.Failure(Error.Conflict("Report.AlreadyResolved", "This report has already been resolved."));

		Status = ReportStatus.Dismissed;
		ResolvedByUserId = resolvedByUserId;
		ResolvedOn = resolvedOn;
		return Result.Success();
	}

	public Result MarkActioned(UserId resolvedByUserId, DateTimeOffset resolvedOn)
	{
		if (Status != ReportStatus.Open)
			return Result.Failure(Error.Conflict("Report.AlreadyResolved", "This report has already been resolved."));

		Status = ReportStatus.Actioned;
		ResolvedByUserId = resolvedByUserId;
		ResolvedOn = resolvedOn;
		return Result.Success();
	}

	// Idempotent by design (unlike Dismiss/MarkActioned) - a report's
	// resolution status is independent of whether its target account still
	// exists, so this just stamps the timestamp regardless of current state.
	public void MarkTargetDeleted(DateTimeOffset deletedOn)
	{
		TargetDeletedOn = deletedOn;
	}
}
