using Application.Common.Exceptions;
using Domain.Notifications;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration
	: IEntityTypeConfiguration<Notification>
{
	public void Configure(
		EntityTypeBuilder<Notification> builder)
	{
		builder.HasKey(n => n.Id);

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_notification_kind_valid",
			"kind IN ('EngagementCreated', 'EngagementConfirmed', 'EngagementCancelled', 'EngagementWithdrawn', " +
			"'OpportunityUpdated', 'OpportunityDeleted', 'OpportunityUnpublished', 'OpportunityCancelled', 'InvitationReceived', " +
			"'NewMatchingOpportunity', 'InvitationAccepted', 'InvitationDeclined', 'FeedbackSubmitted')"));

		builder.Property(n => n.Id)
			.HasConversion(
				id => id.Value,
				guid => NotificationId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(n => n.RecipientId)
			.HasConversion(
				id => id.Value,
				guid => UserId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(n => n.Kind)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(n => n.RelatedEntityId)
			.IsRequired();

		builder.Property(n => n.IsRead)
			.IsRequired();

		builder.Property(n => n.ReadOn);

		builder.Property(n => n.CreatedOn);

		builder.Property(n => n.ModifiedOn);

		builder.Ignore(n => n.Events);

		builder.HasIndex(n => n.RecipientId);

		builder.HasIndex(n => new { n.RecipientId, n.IsRead });

		builder.HasIndex(n => new { n.RecipientId, n.CreatedOn });

		// Supports NotificationRetentionJob's global prune scan (not scoped to a
		// single recipient like the indexes above) - see einsatzbereit#1209. The
		// unread branch still filters on CreatedOn; the read branch filters on
		// ReadOn (#1725) - kept as two indexes since IsRead partitions the table
		// into disjoint branches that each want a different second column.
		builder.HasIndex(n => new { n.IsRead, n.CreatedOn });

		builder.HasIndex(n => new { n.IsRead, n.ReadOn });
	}
}
