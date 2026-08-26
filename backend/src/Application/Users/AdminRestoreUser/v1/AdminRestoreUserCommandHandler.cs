using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.AuditLogs;
using Domain.Primitives;

namespace Application.Users.AdminRestoreUser.v1;

internal sealed class AdminRestoreUserCommandHandler(
	IApplicationDbContext dbContext,
	IFileStorageService fileStorage)
	: ICommandHandler<AdminRestoreUserCommand, bool>
{
	public async ValueTask<bool> Handle(
		AdminRestoreUserCommand request,
		CancellationToken cancellationToken = default)
	{
		var userId = Domain.Users.UserId.Create(request.UserId).GetValueOrThrow();

		var user = await dbContext.FindUserIncludingDeletedAsync(userId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("User.NotFound", $"User '{request.UserId}' not found."));

		user.Restore().ThrowIfFailure();

		if (user.AvatarUrl is not null)
		{
			var avatarObjectKey = fileStorage.GetObjectKeyFromPublicUrl(user.AvatarUrl);
			if (avatarObjectKey is not null)
			{
				try
				{
					await fileStorage.UnquarantineAsync(avatarObjectKey, cancellationToken);
				}
				catch
				{
					// Object may already be public (never actually quarantined, e.g. a
					// row shadow-deleted before this existed) or storage may be
					// transiently unavailable; continue - the DB-level restore is what
					// actually makes the user visible again.
				}
			}
		}

		var auditLog = AuditLog.Create(
			request.AdminUserId,
			AuditActionType.UserRestored,
			AuditSubjectType.User,
			request.UserId);
		await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);

		return true;
	}
}
