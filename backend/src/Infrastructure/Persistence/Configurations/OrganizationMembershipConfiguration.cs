using Domain.Organizations;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
	public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
	{
		builder.HasKey(m => m.Id);

		builder.Property(m => m.Id)
			.HasConversion(
				id => id.Value,
				guid => new OrganizationMembershipId(guid))
			.ValueGeneratedNever();

		builder.Property(m => m.OrganizationId)
			.HasConversion(
				id => id.Value,
				guid => new OrganizationId(guid))
			.IsRequired();

		builder.Property(m => m.UserId)
			.HasConversion(
				id => id.Value,
				guid => new UserId(guid))
			.IsRequired();

		builder.Property(m => m.Role)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(m => m.CreatedOn).IsRequired();
		builder.Property(m => m.ModifiedOn);

		builder.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique();

		builder.Ignore(m => m.Events);
	}
}
