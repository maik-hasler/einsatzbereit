using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Application.Engagements;
using Application.VolunteerOpportunities.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;
using Microsoft.Extensions.Logging;

namespace Application.Organizations.DeleteOrganization.v1;

internal sealed class DeleteOrganizationCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService,
	IEngagementReadRepository engagementReadRepository,
	IFileStorageService fileStorage,
	ILogger<DeleteOrganizationCommandHandler> logger)
	: ICommandHandler<DeleteOrganizationCommand, bool>
{
	public async ValueTask<bool> Handle(
		DeleteOrganizationCommand request,
		CancellationToken cancellationToken = default)
	{
		var organizationId = OrganizationId.Create(request.OrganizationId).GetValueOrThrow();

		var organization = await dbContext.Organizations.FindAsync(organizationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", $"Organization '{request.OrganizationId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId,
			request.RequestingUserId,
			cancellationToken);

		var members = await keycloakOrganizationService.GetMembersAsync(
			request.OrganizationId, cancellationToken);

		if (members.Count > 1)
			throw new ResultFailureException(Error.Conflict(
				"Organization.MultipleMembers",
				"Conflict: only the organization's sole remaining member can delete it. Remove the other members first."));

		var blockingOpportunities = await dbContext.GetBlockingOpportunitiesForOrganizationAsync(
			organizationId, cancellationToken);

		if (blockingOpportunities.Count > 0)
		{
			var titles = string.Join(", ", blockingOpportunities.Select(o => $"'{o.TitleDe}'"));
			throw new ResultFailureException(Error.Conflict(
				"Organization.HasBlockingOpportunities",
				$"Conflict: cannot delete organization while it has opportunities with future time slots or active engagements: {titles}. Resolve or cancel these first."));
		}

		await dbContext.RemoveMembershipsForOrganizationAsync(organizationId, cancellationToken);
		await dbContext.RemoveDashboardLayoutsForOrganizationAsync(organizationId, cancellationToken);

		var remainingOrganizerOrgs = await dbContext.GetOrganizerOrganizationsAsync(
			request.RequestingUserId, cancellationToken);

		if (remainingOrganizerOrgs.Count == 0)
			await keycloakOrganizationService.RevokeOrganizerRoleAsync(request.RequestingUserId.Value, cancellationToken);

		// Without this, opportunities survive as orphan rows with a dangling
		// organization_id - there is no FK to cascade the delete at the DB level
		// (#1153). Only opportunities with no future slots and no active
		// engagements can reach this point (the blocking check above), so the
		// shared helper's engagement cascade is a no-op here; it still resolves
		// any open abuse reports against each opportunity before deleting it.
		var organizationOpportunities = await dbContext.GetOpportunitiesForOrganizationAsync(
			organizationId, cancellationToken);
		var now = DateTimeOffset.UtcNow;
		foreach (var opportunity in organizationOpportunities)
		{
			await VolunteerOpportunityDeletionHelper.DeleteAsync(
				dbContext,
				engagementReadRepository,
				fileStorage,
				opportunity,
				opportunity.Id,
				request.RequestingUserId,
				now,
				logger,
				cancellationToken);
		}

		var openReports = await dbContext.GetOpenReportsForTargetAsync(
			ReportTargetType.Organization, organizationId.Value, cancellationToken);
		foreach (var report in openReports)
		{
			report.MarkActioned(request.RequestingUserId, DateTimeOffset.UtcNow).ThrowIfFailure();
		}

		if (organization.LogoUrl is not null)
		{
			var logoObjectKey = fileStorage.GetObjectKeyFromPublicUrl(organization.LogoUrl);
			if (logoObjectKey is not null)
			{
				try
				{
					await fileStorage.DeleteAsync(logoObjectKey, cancellationToken);
				}
				catch
				{
					// Object may already be gone or storage may be transiently unavailable; continue.
				}
			}
		}

		organization.Delete();
		dbContext.Organizations.Delete(organization);

		return true;
	}
}
