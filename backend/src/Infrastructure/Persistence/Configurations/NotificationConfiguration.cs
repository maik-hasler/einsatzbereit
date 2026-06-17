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

		builder.Property(n => n.Id)
			.HasConversion(
				id => id.Value,
				guid => new NotificationId(guid))
			.ValueGeneratedNever();

		builder.Property(n => n.RecipientId)
			.HasConversion(
				id => id.Value,
				guid => new UserId(guid))
			.IsRequired();

		builder.Property(n => n.Kind)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(n => n.RelatedEntityId)
			.IsRequired();

		builder.Property(n => n.IsRead)
			.IsRequired();

		builder.Property(n => n.CreatedOn);

		builder.Property(n => n.ModifiedOn);

		builder.Ignore(n => n.Events);

		builder.HasIndex(n => n.RecipientId);

		builder.HasIndex(n => new { n.RecipientId, n.IsRead });

		builder.HasIndex(n => new { n.RecipientId, n.CreatedOn });
	}
}
