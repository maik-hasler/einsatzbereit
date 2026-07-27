using Domain.Primitives;

namespace Domain.Reports;

public readonly record struct ReportId : IValueObject
{
	public Guid Value { get; }

	private ReportId(Guid value) => Value = value;

	public static Result<ReportId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<ReportId>(Error.Validation("ReportId.Empty", "ReportId must not be empty."))
			: Result.Success(new ReportId(value));

	public static ReportId New() => new(Guid.CreateVersion7());
}
