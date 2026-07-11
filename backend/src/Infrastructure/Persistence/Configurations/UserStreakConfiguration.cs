using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class UserStreakConfiguration
	: IEntityTypeConfiguration<UserStreak>
{
	public void Configure(
		EntityTypeBuilder<UserStreak> builder)
	{
		builder.HasKey(s => s.Id);

		builder.Property(s => s.Id)
			.HasConversion(
				id => id.Value,
				guid => new UserStreakId(guid))
			.ValueGeneratedNever();

		builder.Property(s => s.UserId)
			.HasConversion(
				id => id.Value,
				guid => new UserId(guid))
			.IsRequired();

		builder.Property(s => s.LoginStreak)
			.HasDefaultValue(0)
			.IsRequired();

		builder.Property(s => s.LastLoginDate);

		builder.Property(s => s.ActivityStreak)
			.HasDefaultValue(0)
			.IsRequired();

		builder.Property(s => s.LastActiveIsoWeek);

		builder.Property(s => s.LastActiveIsoYear);

		builder.Property(s => s.TotalConfirmedEngagements)
			.HasDefaultValue(0)
			.IsRequired();

		builder.Property(s => s.CreatedOn);

		builder.Property(s => s.ModifiedOn);

		builder.Ignore(s => s.Events);

		builder.HasIndex(s => s.UserId).IsUnique();
	}
}
