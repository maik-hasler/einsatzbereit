using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;

namespace Application.Organizations.DeleteOrganization.v1;

internal sealed class DeleteOrganizationCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService,
	IEngagementReadRepository engagementReadRepository)
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
			var titles = string.Join(", ", blockingOpportunities.Select(o => $"'{o.Title}'"));
			throw new ResultFailureException(Error.Conflict(
				"Organization.HasBlockingOpportunities",
				$"Conflict: cannot delete organization while it has opportunities with future time slots or active engagements: {titles}. Resolve or cancel these first."));
		}

		await keycloakOrganizationService.DeleteOrganizationAsync(
			request.OrganizationId, cancellationToken);

		await dbContext.RemoveMembershipsForOrganizationAsync(organizationId, cancellationToken);
		await dbContext.RemoveDashboardLayoutsForOrganizationAsync(organizationId, cancellationToken);

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
				opportunity,
				opportunity.Id,
				request.RequestingUserId,
				now,
				cancellationToken);
		}

		// Resolves any open abuse reports against the organization itself - it
		// can't be reported-and-open once it no longer exists (#1075).
		var openReports = await dbContext.GetOpenReportsForTargetAsync(
			ReportTargetType.Organization, organizationId.Value, cancellationToken);
		foreach (var report in openReports)
		{
			report.MarkActioned(request.RequestingUserId, DateTimeOffset.UtcNow).ThrowIfFailure();
		}

		dbContext.Organizations.Delete(organization);

		return true;
	}
}
