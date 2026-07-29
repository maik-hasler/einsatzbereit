using Application.Common.Exceptions;
using Domain.VolunteerOpportunities;
using Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class VolunteerOpportunityConfiguration
	: IEntityTypeConfiguration<VolunteerOpportunity>
{
	public void Configure(
		EntityTypeBuilder<VolunteerOpportunity> builder)
	{
		builder.HasKey(vo => vo.Id);

		builder.Property(vo => vo.Id)
			.HasConversion(
				id => id.Value,
				guid => VolunteerOpportunityId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(vo => vo.OrganizationId)
			.HasConversion(
				id => id.Value,
				guid => OrganizationId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(vo => vo.Title)
			.HasMaxLength(VolunteerOpportunity.MaxTitleLength)
			.IsRequired();

		builder.Property(vo => vo.Description)
			.HasMaxLength(VolunteerOpportunity.MaxDescriptionLength)
			.IsRequired();

		builder.Property(vo => vo.IsRemote)
			.IsRequired();

		builder.OwnsOne(vo => vo.Address, address =>
		{
			address.Property(a => a.Street).IsRequired();
			address.Property(a => a.HouseNumber).IsRequired();
			address.Property(a => a.ZipCode).HasMaxLength(5).IsRequired();
			address.Property(a => a.City).IsRequired();
			address.Property(a => a.Latitude);
			address.Property(a => a.Longitude);

			address.HasIndex(a => new { a.Latitude, a.Longitude });

			// Stored generated column mirroring LOWER(address_city) so the GIN trigram index
			// below can accelerate the case-insensitive substring search in
			// VolunteerOpportunityReadRepository.GetPagedSummariesAsync (city.ToLower().Contains(...)).
			address.Property<string>("CityNormalized")
				.HasComputedColumnSql("lower(address_city)", stored: true);

			address.HasIndex("CityNormalized")
				.HasDatabaseName("ix_volunteer_opportunity_city_normalized_trgm")
				.HasMethod("gin")
				.HasOperators("gin_trgm_ops");
		});

		builder.Property(vo => vo.Occurrence)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(vo => vo.ParticipationType)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(vo => vo.CheckInMethod)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(vo => vo.Category)
			.HasConversion<string>()
			.IsRequired(false);

		builder.Property(vo => vo.Status)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(vo => vo.BannerImageUrl);

		builder.Property(vo => vo.Color);

		builder.PrimitiveCollection(vo => vo.Tags)
			.HasColumnType("text[]");

		builder.Property(vo => vo.CheckInPin);

		builder.Property(vo => vo.CreatedOn);

		builder.Property(vo => vo.ModifiedOn);

		builder.Property(vo => vo.IsDeleted)
			.HasDefaultValue(false);

		builder.Property(vo => vo.DeletedOn);

		builder.HasQueryFilter(vo => !vo.IsDeleted);

		builder.HasMany(vo => vo.TimeSlots)
			.WithOne()
			.HasForeignKey("volunteer_opportunity_id")
			.IsRequired();

		builder.HasIndex(vo => vo.OrganizationId);

		builder.HasIndex(vo => new { vo.Status, vo.CreatedOn });

		builder.HasIndex(vo => vo.Tags)
			.HasMethod("gin");

		builder.Ignore(vo => vo.Events);
	}
}
