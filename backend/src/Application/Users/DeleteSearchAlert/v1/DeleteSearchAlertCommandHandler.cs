using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;

namespace Application.Users.DeleteSearchAlert.v1;

internal sealed class DeleteSearchAlertCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<DeleteSearchAlertCommand, bool>
{
	public async ValueTask<bool> Handle(
		DeleteSearchAlertCommand request,
		CancellationToken cancellationToken = default)
	{
		var alert = await dbContext.GetSearchAlertForUserAsync(request.UserId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("SearchAlert.NotFound", "No active search alert exists for this user."));

		dbContext.SearchAlerts.Delete(alert);

		return true;
	}
}
