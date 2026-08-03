using Application.AuditLogs.ListAuditLogs.v1;
using Application.Common.Pagination;

namespace Application.AuditLogs;

public interface IAdminAuditLogReadRepository
{
	ValueTask<PagedList<AuditLogEntry>> GetAuditLogsPagedAsync(
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default);
}
