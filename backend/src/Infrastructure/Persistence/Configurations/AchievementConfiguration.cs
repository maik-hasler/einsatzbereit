using Application.Common.Exceptions;
using Domain.Achievements;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class AchievementConfiguration
	: IEntityTypeConfiguration<Achievement>
{
	public void Configure(
		EntityTypeBuilder<Achievement> builder)
	{
		builder.HasKey(a => a.Id);

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_achievement_type_valid",
			"type IN ('Milestone', 'Streak', 'Hidden')"));

		builder.Property(a => a.Id)
			.HasConversion(
				id => id.Value,
				guid => AchievementId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(a => a.UserId)
			.HasConversion(
				id => id.Value,
				guid => UserId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(a => a.Type)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(a => a.Key)
			.HasMaxLength(100);

		builder.Property(a => a.Name)
			.IsRequired()
			.HasMaxLength(200);

		builder.Property(a => a.Description)
			.IsRequired()
			.HasMaxLength(1000);

		builder.Property(a => a.UnlockedAt)
			.IsRequired();

		builder.Property(a => a.CreatedOn);

		builder.Property(a => a.ModifiedOn);

		builder.Ignore(a => a.Events);

		builder.HasIndex(a => a.UserId);

		builder.HasIndex(a => new { a.UserId, a.Name }).IsUnique();
	}
}
