using Domain.Primitives;
using Domain.Users;

namespace Domain.Reports;

public sealed class Report
	: AggregateRoot<ReportId>,
		IAuditableEntity
{
	public const int MaxDetailLength = 1000;

	public ReportedContentType ContentType { get; private set; }

	public Guid ContentId { get; private set; }

	public UserId ReporterId { get; private set; }

	public ReportReason Reason { get; private set; }

	public string? Detail { get; private set; }

	public ReportStatus Status { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

#pragma warning disable CS8618
	private Report() : base(default) { }
#pragma warning restore CS8618

	private Report(
		ReportId id,
		ReportedContentType contentType,
		Guid contentId,
		UserId reporterId,
		ReportReason reason,
		string? detail)
		: base(id)
	{
		ContentType = contentType;
		ContentId = contentId;
		ReporterId = reporterId;
		Reason = reason;
		Detail = detail;
		Status = ReportStatus.Pending;
	}

	public static Result<Report> Create(
		ReportedContentType contentType,
		Guid contentId,
		UserId reporterId,
		ReportReason reason,
		string? detail)
	{
		if (reason == ReportReason.Other && string.IsNullOrWhiteSpace(detail))
			return Result.Failure<Report>(Error.Validation(
				"Report.DetailRequired",
				"A detail description is required when the reason is 'Other'."));

		if (detail is { Length: > MaxDetailLength })
			return Result.Failure<Report>(Error.Validation(
				"Report.DetailTooLong",
				$"Detail must not exceed {MaxDetailLength} characters."));

		return Result.Success(new Report(ReportId.New(), contentType, contentId, reporterId, reason, detail));
	}

	public Result Resolve()
	{
		if (Status != ReportStatus.Pending)
			return Result.Failure(Error.Conflict("Report.NotPending", "Only pending reports can be resolved."));

		Status = ReportStatus.Resolved;
		return Result.Success();
	}

	public Result Dismiss()
	{
		if (Status != ReportStatus.Pending)
			return Result.Failure(Error.Conflict("Report.NotPending", "Only pending reports can be dismissed."));

		Status = ReportStatus.Dismissed;
		return Result.Success();
	}
}
