using Application.Common.Exceptions;
using Domain.Reports;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class ReportConfiguration
	: IEntityTypeConfiguration<Report>
{
	public void Configure(
		EntityTypeBuilder<Report> builder)
	{
		builder.HasKey(r => r.Id);

		builder.Property(r => r.Id)
			.HasConversion(
				id => id.Value,
				guid => ReportId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(r => r.ContentType)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(r => r.ContentId)
			.IsRequired();

		builder.Property(r => r.ReporterId)
			.HasConversion(
				id => id.Value,
				guid => UserId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(r => r.Reason)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(r => r.Detail)
			.HasMaxLength(Report.MaxDetailLength);

		builder.Property(r => r.Status)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(r => r.CreatedOn).IsRequired();

		builder.Property(r => r.ModifiedOn);

		builder.HasIndex(r => new { r.ContentType, r.ContentId });

		builder.HasIndex(r => r.Status);

		builder.Ignore(r => r.Events);
	}
}
