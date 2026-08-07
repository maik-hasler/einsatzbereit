using Application.Common.Exceptions;
using Domain.Primitives;
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

		builder.ToTable(t =>
		{
			t.HasCheckConstraint(
				"ck_report_target_type_valid",
				"target_type IN ('VolunteerOpportunity', 'Organization', 'User')");
			t.HasCheckConstraint(
				"ck_report_reason_valid",
				"reason IN ('Spam', 'IllegalContent', 'Fraud', 'Harassment', 'Other')");
			t.HasCheckConstraint(
				"ck_report_status_valid",
				"status IN ('Open', 'Dismissed', 'Actioned')");
		});

		builder.Property(r => r.Id)
			.HasConversion(
				id => id.Value,
				guid => ReportId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(r => r.TargetType)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(r => r.TargetId)
			.IsRequired();

		builder.Property(r => r.ReporterId)
			.HasConversion(
				id => id.Value,
				guid => UserId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(r => r.Reason)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(r => r.Details)
			.HasMaxLength(Report.MaxDetailsLength);

		builder.Property(r => r.Status)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(r => r.ResolvedByUserId)
			.HasConversion(
				id => id.HasValue ? id.Value.Value : (Guid?)null,
				guid => guid.HasValue ? UserId.Create(guid.Value).GetValueOrThrow() : null);

		builder.Property(r => r.ResolvedOn);

		builder.Property(r => r.CreatedOn);

		builder.Property(r => r.ModifiedOn);

		builder.Property(r => r.TargetDeletedOn);

		builder.Ignore(r => r.Events);

		builder.HasIndex(r => new { r.TargetType, r.TargetId });

		builder.HasIndex(r => r.Status);

		// Supports AbuseReportRetentionJob's global prune scan (#1725).
		builder.HasIndex(r => r.TargetDeletedOn);
	}
}
