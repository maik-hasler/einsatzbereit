using System.Text.Json;
using Application.Common.Exceptions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration
	: IEntityTypeConfiguration<User>
{
	public void Configure(
		EntityTypeBuilder<User> builder)
	{
		builder.HasKey(u => u.Id);

		builder.Property(u => u.Id)
			.HasConversion(
				id => id.Value,
				guid => UserId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(u => u.AvatarUrl);

		builder.Property(u => u.Bio)
			.HasMaxLength(1000);

		builder.Property(u => u.Phone)
			.HasMaxLength(30);

		var listComparer = new ValueComparer<IReadOnlyList<string>>(
			(a, b) => a != null && b != null && a.SequenceEqual(b),
			v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
			v => v.ToList());

		builder.Property(u => u.Skills)
			.HasConversion(
				list => JsonSerializer.Serialize(list, JsonSerializerOptions.Default),
				json => JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonSerializerOptions.Default) ?? Array.Empty<string>())
			.HasColumnType("text")
			.Metadata.SetValueComparer(listComparer);

		builder.Property(u => u.Languages)
			.HasConversion(
				list => JsonSerializer.Serialize(list, JsonSerializerOptions.Default),
				json => JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonSerializerOptions.Default) ?? Array.Empty<string>())
			.HasColumnType("text")
			.Metadata.SetValueComparer(listComparer);

		builder.Property(u => u.PreferredContact)
			.HasConversion<string>();

		builder.Property(u => u.PreferredLanguage)
			.HasMaxLength(5);

		// gen_random_uuid() backfills existing rows when this column is added by
		// migration; every new User.Create() supplies its own value explicitly
		// (see User's constructor), so the DB default never fires for inserts.
		builder.Property(u => u.UnsubscribeToken)
			.HasDefaultValueSql("gen_random_uuid()");

		builder.Property(u => u.NotifyOnNewSignUp)
			.HasDefaultValue(true);

		builder.Property(u => u.NotifyOnWithdrawal)
			.HasDefaultValue(true);

		builder.Property(u => u.NotifyOnEngagementConfirmed)
			.HasDefaultValue(true);

		builder.Property(u => u.NotifyOnEngagementCancelled)
			.HasDefaultValue(true);

		builder.Property(u => u.NotifyOnEngagementReminder)
			.HasDefaultValue(true);

		builder.Property(u => u.IsDeleted)
			.HasDefaultValue(false);

		builder.Property(u => u.DeletedOn);

		builder.HasQueryFilter(u => !u.IsDeleted);

		builder.Ignore(u => u.Events);
	}
}
