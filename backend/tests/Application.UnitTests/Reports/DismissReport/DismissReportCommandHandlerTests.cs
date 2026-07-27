using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Reports.DismissReport.v1;
using AwesomeAssertions;
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

	public DismissReportCommandHandlerTests()
	{
		_dbContext.Reports.Returns(_reportRepo);
		_sut = new DismissReportCommandHandler(_dbContext);
	}

	private static Report CreatePendingReport() =>
		Report.Create(ReportedContentType.VolunteerOpportunity, Guid.NewGuid(), UserId.New(), ReportReason.Spam, null).Value;

	[Test]
	public async Task Handle_ShouldDismissReport_WhenPending(
		CancellationToken cancellationToken)
	{
		// Arrange
		var report = CreatePendingReport();
		_reportRepo.FindAsync(report.Id, cancellationToken).Returns(report);

		// Act
		var result = await _sut.Handle(new DismissReportCommand(report.Id.Value), cancellationToken);

		// Assert
		result.Should().BeTrue();
		report.Status.Should().Be(ReportStatus.Dismissed);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenReportNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var reportId = Guid.NewGuid();
		_reportRepo.FindAsync(ReportId.Create(reportId).GetValueOrThrow(), cancellationToken).Returns((Report?)null);

		// Act
		Func<Task> act = async () => await _sut.Handle(new DismissReportCommand(reportId), cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{reportId}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenReportAlreadyResolved(
		CancellationToken cancellationToken)
	{
		// Arrange
		var report = CreatePendingReport();
		report.Resolve();
		_reportRepo.FindAsync(report.Id, cancellationToken).Returns(report);

		// Act
		Func<Task> act = async () => await _sut.Handle(new DismissReportCommand(report.Id.Value), cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*dismissed*");
	}
}
