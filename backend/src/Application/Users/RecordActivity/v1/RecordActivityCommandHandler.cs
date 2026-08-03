using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Users;

namespace Application.Users.RecordActivity.v1;

internal sealed class RecordActivityCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<RecordActivityCommand, bool>
{
	public async ValueTask<bool> Handle(
		RecordActivityCommand request,
		CancellationToken cancellationToken = default)
	{
		var streak = await dbContext.GetOrCreateUserStreakAsync(request.UserId, cancellationToken);

		streak.RecordActivity(request.IsoYear, request.IsoWeek);
		return true;
	}
}
