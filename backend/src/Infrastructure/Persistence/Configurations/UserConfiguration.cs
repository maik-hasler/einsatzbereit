using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
				guid => new UserId(guid))
			.ValueGeneratedNever();

		builder.Property(u => u.Bio);

		builder.Property(u => u.Skills)
			.HasConversion(
				list => string.Join('|', list),
				raw => (IReadOnlyList<string>)(raw == "" ? Array.Empty<string>() : raw.Split('|', StringSplitOptions.RemoveEmptyEntries)))
			.HasColumnType("text");

		builder.Property(u => u.Languages)
			.HasConversion(
				list => string.Join('|', list),
				raw => (IReadOnlyList<string>)(raw == "" ? Array.Empty<string>() : raw.Split('|', StringSplitOptions.RemoveEmptyEntries)))
			.HasColumnType("text");

		builder.Property(u => u.PreferredContact)
			.HasConversion<string>();

		builder.Ignore(u => u.Events);
	}
}
