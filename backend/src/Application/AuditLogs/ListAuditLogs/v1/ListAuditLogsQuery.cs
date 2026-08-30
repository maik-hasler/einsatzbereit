using Application.Common.Messaging;
using Application.Common.Pagination;
using Domain.AuditLogs;

namespace Application.AuditLogs.ListAuditLogs.v1;

/// <param name="From">Inclusive lower bound on <c>CreatedOn</c>.</param>
/// <param name="To">Exclusive upper bound on <c>CreatedOn</c>, so a caller filtering "up to and
/// including 3 March" passes local midnight of 4 March and stays free of timezone edge cases.</param>
public sealed record ListAuditLogsQuery(
	int PageNumber,
	int PageSize,
	AuditActionType? ActionType = null,
	AuditSubjectType? SubjectType = null,
	Guid? ActorUserId = null,
	DateTimeOffset? From = null,
	DateTimeOffset? To = null,
	bool OldestFirst = false)
	: IQuery<PagedList<AuditLogEntry>>;
