using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;

namespace Application.Organizations.AdminDeleteOrganization.v1;

/// <summary>
/// Admin-only takedown of an entire organization: unlike the organizer-triggered
/// <see cref="DeleteOrganization.v1.DeleteOrganizationCommandHandler"/>, this
/// skips the sole-member and no-blocking-opportunities guardrails (those exist
/// to let a lone organizer clean up their own empty org, not to protect an
/// abusive org with active members/content from an admin takedown) and instead
/// force-cascades: every one of the organization's volunteer opportunities is
/// deleted first (notifying volunteers, cancelling engagements, resolving open
/// reports - same as a normal opportunity delete), then the organization itself
/// (see einsatzbereit#1075).
/// </summary>
internal sealed class AdminDeleteOrganizationCommandHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository,
	IKeycloakOrganizationService keycloakOrganizationService)
	: ICommandHandler<AdminDeleteOrganizationCommand, bool>
{
	public async ValueTask<bool> Handle(
		AdminDeleteOrganizationCommand request,
		CancellationToken cancellationToken = default)
	{
		var organizationId = OrganizationId.Create(request.OrganizationId).GetValueOrThrow();

		var organization = await dbContext.Organizations.FindAsync(organizationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", $"Organization '{request.OrganizationId}' not found."));

		var opportunities = await dbContext.GetOpportunitiesForOrganizationAsync(organizationId, cancellationToken);
		foreach (var opportunity in opportunities)
		{
			await VolunteerOpportunityDeletionHelper.DeleteAsync(
				dbContext,
				engagementReadRepository,
				opportunity,
				opportunity.Id,
				request.AdminUserId,
				DateTimeOffset.UtcNow,
				cancellationToken);
		}

		await keycloakOrganizationService.DeleteOrganizationAsync(
			request.OrganizationId, cancellationToken);

		await dbContext.RemoveMembershipsForOrganizationAsync(organizationId, cancellationToken);
		await dbContext.RemoveDashboardLayoutsForOrganizationAsync(organizationId, cancellationToken);

		var openReports = await dbContext.GetOpenReportsForTargetAsync(
			ReportTargetType.Organization, organizationId.Value, cancellationToken);
		foreach (var report in openReports)
		{
			report.MarkActioned(request.AdminUserId, DateTimeOffset.UtcNow).ThrowIfFailure();
		}

		dbContext.Organizations.Delete(organization);

		return true;
	}
}
