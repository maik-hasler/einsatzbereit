using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Reports.DismissReport.v1;
using AwesomeAssertions;
using Domain.AuditLogs;
using Domain.Primitives;
using Domain.Reports;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Reports.DismissReport;

public class DismissReportCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Report, ReportId> _reportRepo =
		Substitute.For<IAggregateRepository<Report, ReportId>>();
	private readonly IAggregateRepository<AuditLog, AuditLogId> _auditLogRepo =
		Substitute.For<IAggregateRepository<AuditLog, AuditLogId>>();
	private readonly DismissReportCommandHandler _sut;

	private static readonly UserId DefaultAdminUserId = UserId.New();

	public DismissReportCommandHandlerTests()
	{
		_dbContext.Reports.Returns(_reportRepo);
		_dbContext.AuditLogs.Returns(_auditLogRepo);
		_sut = new DismissReportCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldDismissReport_WhenOpen(
		CancellationToken cancellationToken)
	{
		// Arrange
		var reportId = Guid.CreateVersion7();
		var report = Report.Create(ReportTargetType.VolunteerOpportunity, Guid.NewGuid(), UserId.New(), ReportReason.Spam, null).Value;
		_reportRepo
			.FindAsync(ReportId.Create(reportId).GetValueOrThrow(), cancellationToken)
			.Returns(report);

		// Act
		var result = await _sut.Handle(new DismissReportCommand(reportId, DefaultAdminUserId), cancellationToken);

		// Assert
		result.Should().BeTrue();
		report.Status.Should().Be(ReportStatus.Dismissed);
		report.ResolvedByUserId.Should().Be(DefaultAdminUserId);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenReportNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var reportId = Guid.CreateVersion7();
		_reportRepo
			.FindAsync(ReportId.Create(reportId).GetValueOrThrow(), cancellationToken)
			.Returns((Report?)null);

		// Act
		Func<Task> act = async () => await _sut.Handle(new DismissReportCommand(reportId, DefaultAdminUserId), cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenReportAlreadyResolved(
		CancellationToken cancellationToken)
	{
		// Arrange
		var reportId = Guid.CreateVersion7();
		var report = Report.Create(ReportTargetType.VolunteerOpportunity, Guid.NewGuid(), UserId.New(), ReportReason.Spam, null).Value;
		report.Dismiss(UserId.New(), DateTimeOffset.UtcNow);
		_reportRepo
			.FindAsync(ReportId.Create(reportId).GetValueOrThrow(), cancellationToken)
			.Returns(report);

		// Act
		Func<Task> act = async () => await _sut.Handle(new DismissReportCommand(reportId, DefaultAdminUserId), cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
	}

	[Test]
	[Arguments(ReportTargetType.VolunteerOpportunity, AuditSubjectType.VolunteerOpportunity)]
	[Arguments(ReportTargetType.Organization, AuditSubjectType.Organization)]
	[Arguments(ReportTargetType.User, AuditSubjectType.User)]
	public async Task Handle_ShouldWriteAuditLogEntryForTheReportedTarget_WhenDismissed(
		ReportTargetType targetType,
		AuditSubjectType expectedSubjectType,
		CancellationToken cancellationToken)
	{
		// Arrange
		var reportId = Guid.CreateVersion7();
		var targetId = Guid.NewGuid();
		var report = Report.Create(targetType, targetId, UserId.New(), ReportReason.Spam, null).Value;
		_reportRepo
			.FindAsync(ReportId.Create(reportId).GetValueOrThrow(), cancellationToken)
			.Returns(report);

		// Act
		await _sut.Handle(new DismissReportCommand(reportId, DefaultAdminUserId), cancellationToken);

		// Assert
		await _auditLogRepo.Received(1).AddAsync(
			Arg.Is<AuditLog>(a => a!.ActorUserId == DefaultAdminUserId
				&& a.ActionType == AuditActionType.ReportDismissed
				&& a.SubjectType == expectedSubjectType
				&& a.SubjectId == targetId),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotWriteAuditLogEntry_WhenReportAlreadyResolved(
		CancellationToken cancellationToken)
	{
		// Arrange
		var reportId = Guid.CreateVersion7();
		var report = Report.Create(ReportTargetType.Organization, Guid.NewGuid(), UserId.New(), ReportReason.Spam, null).Value;
		report.Dismiss(UserId.New(), DateTimeOffset.UtcNow);
		_reportRepo
			.FindAsync(ReportId.Create(reportId).GetValueOrThrow(), cancellationToken)
			.Returns(report);

		// Act
		Func<Task> act = async () => await _sut.Handle(new DismissReportCommand(reportId, DefaultAdminUserId), cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>();
		await _auditLogRepo.DidNotReceive().AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
	}
}
