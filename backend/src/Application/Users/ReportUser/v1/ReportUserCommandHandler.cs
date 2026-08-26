using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.Reports;

namespace Application.Users.ReportUser.v1;

internal sealed class ReportUserCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<ReportUserCommand, bool>
{
	public async ValueTask<bool> Handle(
		ReportUserCommand request,
		CancellationToken cancellationToken = default)
	{
		var targetUserId = Domain.Users.UserId.Create(request.UserId).GetValueOrThrow();

		if (targetUserId == request.ReporterId)
			throw new ResultFailureException(Error.Validation("Report.CannotReportSelf", "You cannot report yourself."));

		_ = await dbContext.Users.FindAsync(targetUserId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("User.NotFound", $"User '{request.UserId}' not found."));

		var alreadyReported = await dbContext.HasDuplicateReportAsync(
			ReportTargetType.User, request.UserId, request.ReporterId, cancellationToken);
		if (alreadyReported)
			throw new ResultFailureException(Error.Conflict("Report.AlreadyReported", "You have already reported this."));

		var report = Report.Create(
			ReportTargetType.User,
			request.UserId,
			request.ReporterId,
			request.Reason,
			request.Details).GetValueOrThrow();

		await dbContext.Reports.AddAsync(report, cancellationToken);

		return true;
	}
}
