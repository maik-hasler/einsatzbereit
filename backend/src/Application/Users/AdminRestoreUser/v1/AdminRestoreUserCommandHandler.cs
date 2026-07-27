using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;

namespace Application.Users.AdminRestoreUser.v1;

/// <summary>
/// Undoes an admin shadow delete (<see cref="AdminShadowDeleteUser.v1.AdminShadowDeleteUserCommandHandler"/>).
/// </summary>
internal sealed class AdminRestoreUserCommandHandler(
	IApplicationDbContext dbContext)
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

		return true;
	}
}
