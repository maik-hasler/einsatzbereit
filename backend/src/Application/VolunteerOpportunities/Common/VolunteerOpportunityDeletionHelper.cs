using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements;
using Domain.Notifications;
using Domain.Reports;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.Common;

internal static class VolunteerOpportunityDeletionHelper
{
	public static async Task DeleteAsync(
		IApplicationDbContext dbContext,
		IEngagementReadRepository engagementReadRepository,
		VolunteerOpportunity opportunity,
		VolunteerOpportunityId opportunityId,
		UserId actingUserId,
		DateTimeOffset now,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		await ResolveEngagementsAndReportsAsync(
			dbContext, engagementReadRepository, opportunity, opportunityId, actingUserId, now, logger, cancellationToken);

		dbContext.VolunteerOpportunities.Delete(opportunity);
	}

	public static async Task ShadowDeleteAsync(
		IApplicationDbContext dbContext,
		IEngagementReadRepository engagementReadRepository,
		VolunteerOpportunity opportunity,
		VolunteerOpportunityId opportunityId,
		UserId actingUserId,
		DateTimeOffset now,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		await ResolveEngagementsAndReportsAsync(
			dbContext, engagementReadRepository, opportunity, opportunityId, actingUserId, now, logger, cancellationToken);

		opportunity.MarkDeleted(now).ThrowIfFailure();
	}

	private static async Task ResolveEngagementsAndReportsAsync(
		IApplicationDbContext dbContext,
		IEngagementReadRepository engagementReadRepository,
		VolunteerOpportunity opportunity,
		VolunteerOpportunityId opportunityId,
		UserId actingUserId,
		DateTimeOffset now,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		await VolunteerOpportunityEngagementCascadeHelper.NotifyAndCancelActiveEngagementsAsync(
			dbContext,
			engagementReadRepository,
			opportunityId,
			opportunity.TitleDe,
			NotificationKind.OpportunityDeleted,
			"Opportunity was deleted.",

			notifyPerEngagement: true,
			logger,
			cancellationToken);

		var openReports = await dbContext.GetOpenReportsForTargetAsync(
			ReportTargetType.VolunteerOpportunity, opportunityId.Value, cancellationToken);
		foreach (var report in openReports)
		{
			report.MarkActioned(actingUserId, now).ThrowIfFailure();
		}
	}
}
