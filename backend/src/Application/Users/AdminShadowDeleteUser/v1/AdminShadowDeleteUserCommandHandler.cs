using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.Reports;

namespace Application.Users.AdminShadowDeleteUser.v1;

/// <summary>
/// Admin-only takedown of a user's public presence: hides their profile and
/// listings (the query filter in UserConfiguration) without touching their
/// Keycloak account or login - that is <c>SetUserEnabled</c>'s job, a
/// separate and orthogonal admin capability (see einsatzbereit#1075).
/// </summary>
internal sealed class AdminShadowDeleteUserCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<AdminShadowDeleteUserCommand, bool>
{
	public async ValueTask<bool> Handle(
		AdminShadowDeleteUserCommand request,
		CancellationToken cancellationToken = default)
	{
		var userId = Domain.Users.UserId.Create(request.UserId).GetValueOrThrow();
		var now = DateTimeOffset.UtcNow;

		var user = await dbContext.Users.FindAsync(userId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("User.NotFound", $"User '{request.UserId}' not found."));

		var openReports = await dbContext.GetOpenReportsForTargetAsync(
			ReportTargetType.User, userId.Value, cancellationToken);
		foreach (var report in openReports)
		{
			report.MarkActioned(request.AdminUserId, now).ThrowIfFailure();
		}

		user.MarkDeleted(now).ThrowIfFailure();

		return true;
	}
}
