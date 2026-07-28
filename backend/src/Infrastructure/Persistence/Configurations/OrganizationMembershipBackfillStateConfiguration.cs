using Infrastructure.Persistence.StartupTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class OrganizationMembershipBackfillStateConfiguration
	: IEntityTypeConfiguration<OrganizationMembershipBackfillState>
{
	public void Configure(
		EntityTypeBuilder<OrganizationMembershipBackfillState> builder)
	{
		builder.HasKey(s => s.Id);

		builder.Property(s => s.Id).ValueGeneratedNever();

		builder.Property(s => s.CompletedOnUtc).IsRequired();
	}
}
