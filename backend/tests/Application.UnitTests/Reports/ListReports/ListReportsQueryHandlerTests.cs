using Application.Common.Pagination;
using Application.Reports;
using Application.Reports.ListReports.v1;
using AwesomeAssertions;
using Domain.Reports;
using NSubstitute;

namespace Application.UnitTests.Reports.ListReports;

public class ListReportsQueryHandlerTests
{
	private readonly IReportReadRepository _readRepo = Substitute.For<IReportReadRepository>();
	private readonly ListReportsQueryHandler _sut;

	public ListReportsQueryHandlerTests()
	{
		_readRepo
			.GetPagedAsync(Arg.Any<ReportStatus?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<AdminReportSummary>([], 0, 1, 10));
		_sut = new ListReportsQueryHandler(_readRepo);
	}

	[Test]
	public async Task Handle_ShouldReturnReports_FromReadRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var item = new AdminReportSummary(
			Guid.NewGuid(), "VolunteerOpportunity", Guid.NewGuid(), "Titel", Guid.NewGuid(), "Spam", null, "Pending", DateTimeOffset.UtcNow);
		_readRepo
			.GetPagedAsync(ReportStatus.Pending, 1, 10, cancellationToken)
			.Returns(new PagedList<AdminReportSummary>([item], 1, 1, 10));

		// Act
		var result = await _sut.Handle(new ListReportsQuery(ReportStatus.Pending, 1, 10), cancellationToken);

		// Assert
		result.Items.Should().ContainSingle().Which.Should().Be(item);
	}

	[Test]
	public async Task Handle_ShouldPassNullStatus_WhenNotFiltering(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListReportsQuery(null, 1, 10), cancellationToken);

		await _readRepo.Received(1).GetPagedAsync(null, 1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListReportsQuery(null, 0, 10), cancellationToken);

		await _readRepo.Received(1).GetPagedAsync(null, 1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCapExcessivePageSize_ToHundred(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListReportsQuery(null, 1, 5000), cancellationToken);

		await _readRepo.Received(1).GetPagedAsync(null, 1, 100, cancellationToken);
	}
}
