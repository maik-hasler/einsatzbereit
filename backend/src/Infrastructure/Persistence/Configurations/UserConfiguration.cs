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

		builder.Property(u => u.Bio);

		builder.Property(u => u.Phone);

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

		builder.Property(u => u.IsDeleted)
			.HasDefaultValue(false);

		builder.Property(u => u.DeletedOn);

		builder.HasQueryFilter(u => !u.IsDeleted);

		builder.Ignore(u => u.Events);
	}
}
