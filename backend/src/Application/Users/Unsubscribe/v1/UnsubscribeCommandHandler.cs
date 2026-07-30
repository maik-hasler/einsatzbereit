using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;

namespace Application.Users.Unsubscribe.v1;

internal sealed class UnsubscribeCommandHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork)
	: ICommandHandler<UnsubscribeCommand, bool>
{
	public async ValueTask<bool> Handle(
		UnsubscribeCommand request,
		CancellationToken cancellationToken = default)
	{
		var user = await dbContext.Users.FindAsync(request.UserId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("User.NotFound", "User not found."));

		user.Unsubscribe(request.Type, request.Token).ThrowIfFailure();

		await unitOfWork.SaveChangesAsync(cancellationToken);

		return true;
	}
}
