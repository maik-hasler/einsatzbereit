using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;

namespace Application.Invitations.DeclineInvitation.v1;

internal sealed class DeclineInvitationCommandHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork)
	: ICommandHandler<DeclineInvitationCommand, bool>
{
	public async ValueTask<bool> Handle(
		DeclineInvitationCommand request,
		CancellationToken cancellationToken = default)
	{
		var invitation = await dbContext.OrganizationInvitations.FindAsync(request.InvitationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("OrganizationInvitation.NotFound", "Invitation not found."));

		if (invitation.InviteeId != request.UserId)
			throw new ResultFailureException(Error.Forbidden("OrganizationInvitation.NotRecipient", "You are not the recipient of this invitation."));

		invitation.Decline().ThrowIfFailure();
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return true;
	}
}
