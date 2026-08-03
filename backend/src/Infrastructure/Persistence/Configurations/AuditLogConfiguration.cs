using Application.Common.Exceptions;
using Domain.AuditLogs;
using Domain.Primitives;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration
	: IEntityTypeConfiguration<AuditLog>
{
	public void Configure(
		EntityTypeBuilder<AuditLog> builder)
	{
		builder.HasKey(a => a.Id);

		builder.ToTable(t =>
		{
			t.HasCheckConstraint(
				"ck_audit_log_action_type_valid",
				"action_type IN ('UserPromotedToAdmin', 'UserDemotedFromAdmin', 'UserEnabled', 'UserDisabled', 'UserShadowDeleted', 'UserRestored', 'OrganizationShadowDeleted', 'OrganizationRestored', 'VolunteerOpportunityShadowDeleted', 'VolunteerOpportunityRestored', 'EngagementCancelled')");
			t.HasCheckConstraint(
				"ck_audit_log_subject_type_valid",
				"subject_type IN ('User', 'Organization', 'VolunteerOpportunity', 'Engagement')");
		});

		builder.Property(a => a.Id)
			.HasConversion(
				id => id.Value,
				guid => AuditLogId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(a => a.ActorUserId)
			.HasConversion(
				id => id.Value,
				guid => UserId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(a => a.ActionType)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(a => a.SubjectType)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(a => a.SubjectId)
			.IsRequired();

		builder.Property(a => a.Reason)
			.HasMaxLength(AuditLog.MaxReasonLength);

		builder.Property(a => a.CreatedOn);

		builder.Property(a => a.ModifiedOn);

		builder.Ignore(a => a.Events);

		builder.HasIndex(a => a.CreatedOn);
	}
}
