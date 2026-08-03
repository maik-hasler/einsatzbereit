using Domain.Primitives;

namespace Domain.AuditLogs;

public readonly record struct AuditLogId : IValueObject
{
	public Guid Value { get; }

	private AuditLogId(Guid value) => Value = value;

	public static Result<AuditLogId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<AuditLogId>(Error.Validation("AuditLogId.Empty", "AuditLogId must not be empty."))
			: Result.Success(new AuditLogId(value));

	public static AuditLogId New() => new(Guid.CreateVersion7());
}
