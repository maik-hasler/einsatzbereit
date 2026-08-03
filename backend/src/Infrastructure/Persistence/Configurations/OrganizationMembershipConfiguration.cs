using Application.Common.Exceptions;
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

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_organization_membership_role_valid",
			"role IN ('Member', 'Organizer')"));

		builder.Property(m => m.Id)
			.HasConversion(
				id => id.Value,
				guid => OrganizationMembershipId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(m => m.OrganizationId)
			.HasConversion(
				id => id.Value,
				guid => OrganizationId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(m => m.UserId)
			.HasConversion(
				id => id.Value,
				guid => UserId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(m => m.Role)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(m => m.CreatedOn).IsRequired();
		builder.Property(m => m.ModifiedOn);

		builder.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique();

		// Was an unconstrained uuid (#1191) - DeleteOrganizationCommandHandler
		// already removes memberships before deleting the organization, so this
		// is a defense-in-depth backstop for any other deletion path, not a
		// behavior change in the normal flow.
		builder.HasOne<Organization>()
			.WithMany()
			.HasForeignKey(m => m.OrganizationId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Ignore(m => m.Events);
	}
}
