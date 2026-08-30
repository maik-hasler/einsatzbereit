using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.AuditLogs.ListAuditLogs.v1;

internal sealed class ListAuditLogsQueryHandler(
	IAdminAuditLogReadRepository readRepository)
	: IQueryHandler<ListAuditLogsQuery, PagedList<AuditLogEntry>>
{
	private const int MaxPageSize = 100;

	public async ValueTask<PagedList<AuditLogEntry>> Handle(
		ListAuditLogsQuery request,
		CancellationToken cancellationToken = default)
	{
		var pageNumber = Math.Max(1, request.PageNumber);
		var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

		return await readRepository.GetAuditLogsPagedAsync(
			new AuditLogFilter(
				request.ActionType,
				request.SubjectType,
				request.ActorUserId,
				request.From,
				request.To,
				request.OldestFirst),
			pageNumber,
			pageSize,
			cancellationToken);
	}
}
