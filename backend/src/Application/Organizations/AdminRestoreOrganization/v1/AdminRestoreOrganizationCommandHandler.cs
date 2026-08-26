using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.AuditLogs;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.AdminRestoreOrganization.v1;

internal sealed class AdminRestoreOrganizationCommandHandler(
	IApplicationDbContext dbContext,
	IFileStorageService fileStorage)
	: ICommandHandler<AdminRestoreOrganizationCommand, bool>
{
	public async ValueTask<bool> Handle(
		AdminRestoreOrganizationCommand request,
		CancellationToken cancellationToken = default)
	{
		var organizationId = OrganizationId.Create(request.OrganizationId).GetValueOrThrow();

		var organization = await dbContext.FindOrganizationIncludingDeletedAsync(organizationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", $"Organization '{request.OrganizationId}' not found."));

		organization.Restore().ThrowIfFailure();

		if (organization.LogoUrl is not null)
		{
			var logoObjectKey = fileStorage.GetObjectKeyFromPublicUrl(organization.LogoUrl);
			if (logoObjectKey is not null)
			{
				try
				{
					await fileStorage.UnquarantineAsync(logoObjectKey, cancellationToken);
				}
				catch
				{
					// Object may already be public (never actually quarantined, e.g. a
					// row shadow-deleted before this existed) or storage may be
					// transiently unavailable; continue - the DB-level restore is what
					// actually makes the organization visible again.
				}
			}
		}

		var auditLog = AuditLog.Create(
			request.AdminUserId,
			AuditActionType.OrganizationRestored,
			AuditSubjectType.Organization,
			request.OrganizationId);
		await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);

		return true;
	}
}
