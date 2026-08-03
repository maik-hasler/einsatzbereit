using Application.Common.Exceptions;
using Domain.Organizations;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class OrganizationInvitationConfiguration : IEntityTypeConfiguration<OrganizationInvitation>
{
	public void Configure(EntityTypeBuilder<OrganizationInvitation> builder)
	{
		builder.HasKey(i => i.Id);

		builder.ToTable(t =>
		{
			t.HasCheckConstraint(
				"ck_organization_invitation_intended_role_valid",
				"intended_role IN ('Member', 'Organizer')");
			t.HasCheckConstraint(
				"ck_organization_invitation_status_valid",
				"status IN ('Pending', 'Accepted', 'Declined', 'Expired')");
		});

		builder.Property(i => i.Id)
			.HasConversion(
				id => id.Value,
				guid => OrganizationInvitationId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(i => i.OrganizationId)
			.HasConversion(
				id => id.Value,
				guid => OrganizationId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(i => i.InviteeId)
			.HasConversion(
				id => id.Value,
				guid => UserId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(i => i.InvitedById)
			.HasConversion(
				id => id.Value,
				guid => UserId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(i => i.IntendedRole)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(i => i.Status)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(i => i.ExpiresOn).IsRequired();

		builder.Property(i => i.CreatedOn).IsRequired();
		builder.Property(i => i.ModifiedOn);

		builder.HasIndex(i => i.InviteeId);
		builder.HasIndex(i => i.OrganizationId);

		// Only one Pending invitation per (org, invitee) at a time (#1202) - lets
		// TryCreateInvitationAsync's "INSERT ... ON CONFLICT (organization_id,
		// invitee_id) WHERE status = 'Pending'" infer this exact partial index.
		// Scoped to Pending only: an org can freely re-invite someone whose prior
		// invitation already resolved (Accepted/Declined/Expired).
		builder.HasIndex(i => new { i.OrganizationId, i.InviteeId })
			.IsUnique()
			.HasFilter("status = 'Pending'");

		// Two concurrent Accept calls for the same invitation both read
		// Status=Pending and both pass the guard - this makes the loser's save
		// fail with a 409 instead of silently double-processing (#1196, #1202).
		// See EngagementConfiguration for why this is a plain IsRowVersion() uint
		// rather than the removed UseXminAsConcurrencyToken() helper.
		builder.Property<uint>("Version").IsRowVersion();

		// Was an unconstrained uuid (#1191): unlike memberships and dashboard
		// layouts, DeleteOrganizationCommandHandler never cleaned up invitations
		// for a deleted organization, so a stale invitee could accept a
		// long-gone organization (AcceptInvitationCommandHandler never checks
		// the organization still exists). This closes that gap at the DB level.
		builder.HasOne<Organization>()
			.WithMany()
			.HasForeignKey(i => i.OrganizationId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Ignore(i => i.Events);
	}
}
