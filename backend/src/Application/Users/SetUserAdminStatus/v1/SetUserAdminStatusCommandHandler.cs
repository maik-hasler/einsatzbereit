using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.AuditLogs;
using Domain.Primitives;
using Domain.Users;

namespace Application.Users.SetUserAdminStatus.v1;

internal sealed class SetUserAdminStatusCommandHandler(
	IKeycloakUserService keycloakUserService,
	IApplicationDbContext dbContext)
	: ICommandHandler<SetUserAdminStatusCommand, bool>
{
	public async ValueTask<bool> Handle(
		SetUserAdminStatusCommand request,
		CancellationToken cancellationToken = default)
	{
		if (await keycloakUserService.IsServiceAccountAsync(request.TargetUserId, cancellationToken))
			throw new ResultFailureException(Error.Forbidden(
				"Users.CannotModifyServiceAccount",
				"The backend's own service account cannot be promoted or demoted."));

		// Per-actor guard only: this closes the accidental self-lockout footgun,
		// not two different admins racing to demote each other. Accepted as an
		// MVP limitation - recoverable via the Keycloak admin console.
		if (!request.IsAdmin && request.TargetUserId == request.ActingUserId)
			throw new ResultFailureException(Error.Conflict(
				"Users.CannotDemoteSelf",
				"You cannot remove your own admin access."));

		if (request.IsAdmin)
			await keycloakUserService.AssignAdminRoleAsync(request.TargetUserId, cancellationToken);
		else
			await keycloakUserService.RemoveAdminRoleAsync(request.TargetUserId, cancellationToken);

		var actingUserId = UserId.Create(request.ActingUserId).GetValueOrThrow();
		var auditLog = AuditLog.Create(
			actingUserId,
			request.IsAdmin ? AuditActionType.UserPromotedToAdmin : AuditActionType.UserDemotedFromAdmin,
			AuditSubjectType.User,
			request.TargetUserId);
		await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);

		return true;
	}
}
