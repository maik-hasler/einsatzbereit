using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Notifications;
using Domain.Notifications;
using Domain.Reports;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.Common;

/// <summary>
/// Notifies affected volunteers, cancels active engagements, resolves any open
/// abuse reports against the opportunity, and deletes it - shared by both the
/// organizer-triggered and admin-triggered delete flows so a takedown resolves
/// as completely as a self-service delete (see einsatzbereit#1075).
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
		await OpportunityNotificationHelper.NotifyActiveVolunteersAsync(
			dbContext,
			engagementReadRepository,
			opportunityId,
			NotificationKind.OpportunityDeleted,
			cancellationToken);

		var activeEngagements = await dbContext.GetActiveEngagementsForOpportunityAsync(
			opportunityId, cancellationToken);
		foreach (var engagement in activeEngagements)
		{
			engagement.Cancel("Opportunity was deleted.").ThrowIfFailure();
		}

		var openReports = await dbContext.GetOpenReportsForTargetAsync(
			ReportTargetType.VolunteerOpportunity, opportunityId.Value, cancellationToken);
		foreach (var report in openReports)
		{
			report.MarkActioned(actingUserId, now).ThrowIfFailure();
		}

		dbContext.VolunteerOpportunities.Delete(opportunity);
	}
}
