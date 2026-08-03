namespace Application.AuditLogs.ListAuditLogs.v1;

public sealed record AuditLogEntry(
	Guid Id,
	Guid ActorUserId,
	string ActorDisplayName,
	string ActionType,
	string SubjectType,
	Guid SubjectId,
	string? Reason,
	DateTimeOffset CreatedOn);
