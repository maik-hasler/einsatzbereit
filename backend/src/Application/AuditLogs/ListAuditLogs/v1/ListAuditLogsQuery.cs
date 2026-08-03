using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.AuditLogs.ListAuditLogs.v1;

public sealed record ListAuditLogsQuery(
	int PageNumber,
	int PageSize)
	: IQuery<PagedList<AuditLogEntry>>;
