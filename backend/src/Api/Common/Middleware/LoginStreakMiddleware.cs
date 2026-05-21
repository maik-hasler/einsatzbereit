using Application.Common.Messaging;
using Application.Users.RecordLogin.v1;
using Domain.Users;

namespace Api.Common.Middleware;

internal sealed class LoginStreakMiddleware(RequestDelegate next)
{
	private static readonly HashSet<string> _todayUpdated = [];
	private static DateOnly _currentDate = DateOnly.FromDateTime(DateTime.UtcNow);
	private static readonly Lock _lock = new();

	public async Task InvokeAsync(HttpContext context, ISender sender)
	{
		if (context.User.Identity?.IsAuthenticated == true)
		{
			var subClaim = context.User.FindFirst("sub")?.Value;
			if (subClaim is not null && Guid.TryParse(subClaim, out var userId))
			{
				var today = DateOnly.FromDateTime(DateTime.UtcNow);

				bool shouldUpdate;
				lock (_lock)
				{
					if (_currentDate != today)
					{
						_todayUpdated.Clear();
						_currentDate = today;
					}
					shouldUpdate = _todayUpdated.Add(subClaim);
				}

				if (shouldUpdate)
				{
					try
					{
						await sender.Send(
							new RecordLoginCommand(new UserId(userId), today),
							context.RequestAborted);
					}
					catch
					{
						// never fail a request due to streak tracking
					}
				}
			}
		}

		await next(context);
	}
}
