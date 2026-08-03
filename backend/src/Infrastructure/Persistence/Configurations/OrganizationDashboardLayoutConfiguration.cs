using System.Text.Json;
using Application.Common.Exceptions;
using Domain.Organizations;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class OrganizationDashboardLayoutConfiguration
	: IEntityTypeConfiguration<OrganizationDashboardLayout>
{
	private static readonly JsonSerializerOptions WidgetsJsonOptions = new()
	{
		Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
	};

	public void Configure(EntityTypeBuilder<OrganizationDashboardLayout> builder)
	{
		builder.HasKey(l => l.Id);

		builder.Property(l => l.Id)
			.HasConversion(
				id => id.Value,
				guid => OrganizationDashboardLayoutId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(l => l.OrganizationId)
			.HasConversion(
				id => id.Value,
				guid => OrganizationId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(l => l.UserId)
			.HasConversion(
				id => id.Value,
				guid => UserId.Create(guid).GetValueOrThrow())
			.IsRequired();

		var widgetsComparer = new ValueComparer<IReadOnlyList<DashboardWidgetPlacement>>(
			(a, b) => a != null && b != null && a.SequenceEqual(b),
			v => v.Aggregate(0, (h, w) => HashCode.Combine(h, w)),
			v => v.ToList());

		builder.Property(l => l.Widgets)
			.HasConversion(
				list => JsonSerializer.Serialize(list, WidgetsJsonOptions),
				json => JsonSerializer.Deserialize<IReadOnlyList<DashboardWidgetPlacement>>(json, WidgetsJsonOptions) ?? Array.Empty<DashboardWidgetPlacement>())
			.HasColumnType("text")
			.IsRequired()
			.Metadata.SetValueComparer(widgetsComparer);

		builder.Property(l => l.CreatedOn).IsRequired();
		builder.Property(l => l.ModifiedOn);

		builder.HasIndex(l => new { l.OrganizationId, l.UserId }).IsUnique();

		// Was an unconstrained uuid (#1191) - DeleteOrganizationCommandHandler
		// already removes dashboard layouts before deleting the organization, so
		// this is a defense-in-depth backstop for any other deletion path, not a
		// behavior change in the normal flow.
		builder.HasOne<Organization>()
			.WithMany()
			.HasForeignKey(l => l.OrganizationId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Ignore(l => l.Events);
	}
}
