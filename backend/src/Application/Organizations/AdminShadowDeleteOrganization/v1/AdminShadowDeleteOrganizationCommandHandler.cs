using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.Common;
using Domain.AuditLogs;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;
using Microsoft.Extensions.Logging;

namespace Application.Organizations.AdminShadowDeleteOrganization.v1;

/// <summary>
/// Admin-only takedown of an entire organization: unlike the organizer-triggered
/// <see cref="DeleteOrganization.v1.DeleteOrganizationCommandHandler"/>, this
/// skips the sole-member and no-blocking-opportunities guardrails (those exist
/// to let a lone organizer clean up their own empty org, not to protect an
/// abusive org with active members/content from an admin takedown) and shadow-
/// deletes instead of hard-deleting: every one of the organization's volunteer
/// opportunities is marked deleted first (notifying volunteers, cancelling
/// engagements, resolving open reports), then the organization itself. Keycloak
/// membership and dashboard layouts are left untouched so a restore
/// (<see cref="AdminRestoreOrganization.v1.AdminRestoreOrganizationCommandHandler"/>)
/// brings the organization back exactly as it was (see einsatzbereit#1075).
/// </summary>
internal sealed class AdminShadowDeleteOrganizationCommandHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository,
	ILogger<AdminShadowDeleteOrganizationCommandHandler> logger)
	: ICommandHandler<AdminShadowDeleteOrganizationCommand, bool>
{
	public async ValueTask<bool> Handle(
		AdminShadowDeleteOrganizationCommand request,
		CancellationToken cancellationToken = default)
	{
		var organizationId = OrganizationId.Create(request.OrganizationId).GetValueOrThrow();
		var now = DateTimeOffset.UtcNow;

		var organization = await dbContext.Organizations.FindAsync(organizationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", $"Organization '{request.OrganizationId}' not found."));

		var opportunities = await dbContext.GetOpportunitiesForOrganizationAsync(organizationId, cancellationToken);
		foreach (var opportunity in opportunities)
		{
			await VolunteerOpportunityDeletionHelper.ShadowDeleteAsync(
				dbContext,
				engagementReadRepository,
				opportunity,
				opportunity.Id,
				request.AdminUserId,
				now,
				logger,
				cancellationToken);
		}

		var openReports = await dbContext.GetOpenReportsForTargetAsync(
			ReportTargetType.Organization, organizationId.Value, cancellationToken);
		foreach (var report in openReports)
		{
			report.MarkActioned(request.AdminUserId, now).ThrowIfFailure();
		}

		organization.MarkDeleted(now).ThrowIfFailure();

		var auditLog = AuditLog.Create(
			request.AdminUserId,
			AuditActionType.OrganizationShadowDeleted,
			AuditSubjectType.Organization,
			request.OrganizationId);
		await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);

		return true;
	}
}
