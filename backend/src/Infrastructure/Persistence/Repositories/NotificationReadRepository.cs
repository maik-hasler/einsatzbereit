using Application.Notifications;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class NotificationReadRepository(
	ApplicationDbContext dbContext)
	: INotificationReadRepository
{
	public async ValueTask<List<NotificationSummary>> GetByRecipientAsync(
		UserId recipientId,
		CancellationToken cancellationToken = default) =>
		await dbContext.NotificationsQuery
			.Where(n => n.RecipientId == recipientId)
			.OrderByDescending(n => n.CreatedOn)
			.Select(n => new NotificationSummary(
				n.Id.Value,
				n.Kind.ToString(),
				n.RelatedEntityId,
				n.IsRead,
				n.CreatedOn))
			.ToListAsync(cancellationToken);
}
