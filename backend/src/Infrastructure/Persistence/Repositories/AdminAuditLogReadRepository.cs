using Application.AuditLogs;
using Application.AuditLogs.ListAuditLogs.v1;
using Application.Common.Keycloak;
using Application.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class AdminAuditLogReadRepository(
	ApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService)
	: IAdminAuditLogReadRepository
{
	public async ValueTask<PagedList<AuditLogEntry>> GetAuditLogsPagedAsync(
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var totalItems = await dbContext.AuditLogsQuery.CountAsync(cancellationToken);

		var page = await dbContext.AuditLogsQuery
			.OrderByDescending(a => a.CreatedOn)
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var actorIds = page.Select(a => a.ActorUserId.Value).Distinct().ToList();
		var actorDisplayNames = actorIds.Count > 0
			? await keycloakUserService.GetDisplayNamesAsync(actorIds, cancellationToken)
			: new Dictionary<Guid, string>();

		var items = page
			.Select(a => new AuditLogEntry(
				a.Id.Value,
				a.ActorUserId.Value,
				actorDisplayNames.GetValueOrDefault(a.ActorUserId.Value, string.Empty),
				a.ActionType.ToString(),
				a.SubjectType.ToString(),
				a.SubjectId,
				a.Reason,
				a.CreatedOn))
			.ToList();

		return new PagedList<AuditLogEntry>(items, totalItems, pageNumber, pageSize);
	}
}
