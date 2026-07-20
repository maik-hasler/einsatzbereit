using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Domain.Primitives;

namespace Application.Users.SetUserEnabled.v1;

internal sealed class SetUserEnabledCommandHandler(
	IKeycloakUserService keycloakUserService)
	: ICommandHandler<SetUserEnabledCommand, bool>
{
	public async ValueTask<bool> Handle(
		SetUserEnabledCommand request,
		CancellationToken cancellationToken = default)
	{
		if (await keycloakUserService.IsServiceAccountAsync(request.TargetUserId, cancellationToken))
			throw new ResultFailureException(Error.Forbidden(
				"Users.CannotModifyServiceAccount",
				"The backend's own service account cannot be blocked or unblocked."));

		if (!request.Enabled && request.TargetUserId == request.ActingUserId)
			throw new ResultFailureException(Error.Conflict(
				"Users.CannotDisableSelf",
				"You cannot block your own account."));

		await keycloakUserService.SetUserEnabledAsync(request.TargetUserId, request.Enabled, cancellationToken);

		return true;
	}
}
