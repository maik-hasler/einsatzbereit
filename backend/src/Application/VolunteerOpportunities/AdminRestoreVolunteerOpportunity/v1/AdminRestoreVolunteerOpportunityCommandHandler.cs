using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.AuditLogs;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.AdminRestoreVolunteerOpportunity.v1;

internal sealed class AdminRestoreVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext,
	IFileStorageService fileStorage)
	: ICommandHandler<AdminRestoreVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		AdminRestoreVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunityId = VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow();

		var opportunity = await dbContext.FindVolunteerOpportunityIncludingDeletedAsync(opportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		opportunity.Restore().ThrowIfFailure();

		if (opportunity.BannerImageUrl is not null)
		{
			var bannerObjectKey = fileStorage.GetObjectKeyFromPublicUrl(opportunity.BannerImageUrl);
			if (bannerObjectKey is not null)
			{
				try
				{
					await fileStorage.UnquarantineAsync(bannerObjectKey, cancellationToken);
				}
				catch
				{
					// Object may already be public (never actually quarantined, e.g. a
					// row shadow-deleted before this existed) or storage may be
					// transiently unavailable; continue - the DB-level restore is what
					// actually makes the opportunity visible again.
				}
			}
		}

		var auditLog = AuditLog.Create(
			request.AdminUserId,
			AuditActionType.VolunteerOpportunityRestored,
			AuditSubjectType.VolunteerOpportunity,
			request.OpportunityId);
		await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);

		return true;
	}
}
