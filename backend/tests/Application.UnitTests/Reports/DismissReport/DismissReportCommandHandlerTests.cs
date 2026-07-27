using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Reports.DismissReport.v1;
using AwesomeAssertions;
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
	private readonly DismissReportCommandHandler _sut;

	private static readonly UserId DefaultAdminUserId = UserId.New();

	public DismissReportCommandHandlerTests()
	{
		_dbContext.Reports.Returns(_reportRepo);
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
}
