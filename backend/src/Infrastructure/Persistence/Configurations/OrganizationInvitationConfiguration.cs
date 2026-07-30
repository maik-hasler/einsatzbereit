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

		builder.Property(i => i.OrganizationName).IsRequired();

		builder.Property(i => i.InviteeId)
			.HasConversion(
				id => id.Value,
				guid => UserId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(i => i.InviteeName).IsRequired();

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

		builder.Ignore(i => i.Events);
	}
}
