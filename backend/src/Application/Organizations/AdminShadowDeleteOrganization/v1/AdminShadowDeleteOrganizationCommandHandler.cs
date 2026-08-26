using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Application.Engagements;
using Application.VolunteerOpportunities.Common;
using Domain.AuditLogs;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;
using Microsoft.Extensions.Logging;

namespace Application.Organizations.AdminShadowDeleteOrganization.v1;

internal sealed class AdminShadowDeleteOrganizationCommandHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository,
	IFileStorageService fileStorage,
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
				fileStorage,
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

		if (organization.LogoUrl is not null)
		{
			var logoObjectKey = fileStorage.GetObjectKeyFromPublicUrl(organization.LogoUrl);
			if (logoObjectKey is not null)
			{
				try
				{
					await fileStorage.QuarantineAsync(logoObjectKey, cancellationToken);
				}
				catch
				{
					// Object may already be gone, already quarantined, or storage may be
					// transiently unavailable; continue - the DB-level shadow delete is
					// what actually hides the organization from all read paths.
				}
			}
		}

		var auditLog = AuditLog.Create(
			request.AdminUserId,
			AuditActionType.OrganizationShadowDeleted,
			AuditSubjectType.Organization,
			request.OrganizationId);
		await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);

		return true;
	}
}
