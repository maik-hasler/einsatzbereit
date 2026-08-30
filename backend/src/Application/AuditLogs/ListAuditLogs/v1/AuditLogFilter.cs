using Domain.AuditLogs;

namespace Application.AuditLogs.ListAuditLogs.v1;

/// <summary>
/// The narrowing applied to the audit log before paging. Every member is optional; the default
/// instance is the unfiltered, newest-first log the page has always shown.
/// </summary>
/// <param name="From">Inclusive lower bound on <c>CreatedOn</c>.</param>
/// <param name="To">Exclusive upper bound on <c>CreatedOn</c>.</param>
public sealed record AuditLogFilter(
	AuditActionType? ActionType = null,
	AuditSubjectType? SubjectType = null,
	Guid? ActorUserId = null,
	DateTimeOffset? From = null,
	DateTimeOffset? To = null,
	bool OldestFirst = false);
