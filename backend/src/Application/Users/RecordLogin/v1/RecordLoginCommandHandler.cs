using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Users;

namespace Application.Users.RecordLogin.v1;

internal sealed class RecordLoginCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<RecordLoginCommand, bool>
{
	public async ValueTask<bool> Handle(
		RecordLoginCommand request,
		CancellationToken cancellationToken = default)
	{
		var streak = await dbContext.GetUserStreakAsync(request.UserId, cancellationToken);

		if (streak is null)
		{
			streak = UserStreak.Create(request.UserId);
			await dbContext.UserStreaks.AddAsync(streak, cancellationToken);
		}

		streak.RecordLogin(request.Date);
		return true;
	}
}
