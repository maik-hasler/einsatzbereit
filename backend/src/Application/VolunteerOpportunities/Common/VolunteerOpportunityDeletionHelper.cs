using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements;
using Domain.Notifications;
using Domain.Reports;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.Common;

/// <summary>
/// Notifies affected volunteers, cancels active engagements, and resolves any
/// open abuse reports against the opportunity - shared by the organizer-triggered
/// hard delete and the admin-triggered shadow delete flows so a takedown resolves
/// as completely as a self-service delete (see einsatzbereit#1075, einsatzbereit#1423).
/// </summary>
internal static class VolunteerOpportunityDeletionHelper
{
	public static async Task DeleteAsync(
		IApplicationDbContext dbContext,
		IEngagementReadRepository engagementReadRepository,
		VolunteerOpportunity opportunity,
		VolunteerOpportunityId opportunityId,
		UserId actingUserId,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		await ResolveEngagementsAndReportsAsync(
			dbContext, engagementReadRepository, opportunity, opportunityId, actingUserId, now, cancellationToken);

		dbContext.VolunteerOpportunities.Delete(opportunity);
	}

	/// <summary>
	/// Admin takedown counterpart to <see cref="DeleteAsync"/>: marks the
	/// opportunity <see cref="Domain.Primitives.ISoftDeletableEntity.IsDeleted"/>
	/// instead of removing the row, so it disappears from every listing (the
	/// query filter in VolunteerOpportunityConfiguration) while staying
	/// restorable.
	/// </summary>
	public static async Task ShadowDeleteAsync(
		IApplicationDbContext dbContext,
		IEngagementReadRepository engagementReadRepository,
		VolunteerOpportunity opportunity,
		VolunteerOpportunityId opportunityId,
		UserId actingUserId,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		await ResolveEngagementsAndReportsAsync(
			dbContext, engagementReadRepository, opportunity, opportunityId, actingUserId, now, cancellationToken);

		opportunity.MarkDeleted(now).ThrowIfFailure();
	}

	private static async Task ResolveEngagementsAndReportsAsync(
		IApplicationDbContext dbContext,
		IEngagementReadRepository engagementReadRepository,
		VolunteerOpportunity opportunity,
		VolunteerOpportunityId opportunityId,
		UserId actingUserId,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		await VolunteerOpportunityEngagementCascadeHelper.NotifyAndCancelActiveEngagementsAsync(
			dbContext,
			engagementReadRepository,
			opportunityId,
			opportunity.Title,
			NotificationKind.OpportunityDeleted,
			"Opportunity was deleted.",
			cancellationToken);

		var openReports = await dbContext.GetOpenReportsForTargetAsync(
			ReportTargetType.VolunteerOpportunity, opportunityId.Value, cancellationToken);
		foreach (var report in openReports)
		{
			report.MarkActioned(actingUserId, now).ThrowIfFailure();
		}
	}
}
