using Application.Common.Pagination;
using Application.Reports;
using Application.Reports.ListOpenReports.v1;
using AwesomeAssertions;
using NSubstitute;

namespace Application.UnitTests.Reports.ListOpenReports;

public class ListOpenReportsQueryHandlerTests
{
	private readonly IAdminReportReadRepository _readRepo =
		Substitute.For<IAdminReportReadRepository>();
	private readonly ListOpenReportsQueryHandler _sut;

	public ListOpenReportsQueryHandlerTests()
	{
		_readRepo
			.GetOpenPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<AdminReportSummary>([], 0, 1, 10));
		_sut = new ListOpenReportsQueryHandler(_readRepo);
	}

	[Test]
	public async Task Handle_ShouldReturnReports_FromReadRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var item = new AdminReportSummary(Guid.NewGuid(), "VolunteerOpportunity", Guid.NewGuid(), "Titel", "Spam", null, DateTimeOffset.UtcNow);

		_readRepo
			.GetOpenPagedAsync(1, 10, cancellationToken)
			.Returns(new PagedList<AdminReportSummary>([item], 1, 1, 10));

		// Act
		var result = await _sut.Handle(new ListOpenReportsQuery(1, 10), cancellationToken);

		// Assert
		result.Items.Should().ContainSingle().Which.Should().Be(item);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListOpenReportsQuery(0, 10), cancellationToken);

		await _readRepo.Received(1).GetOpenPagedAsync(1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampNegativePageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListOpenReportsQuery(-5, 10), cancellationToken);

		await _readRepo.Received(1).GetOpenPagedAsync(1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageSize_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListOpenReportsQuery(1, 0), cancellationToken);

		await _readRepo.Received(1).GetOpenPagedAsync(1, 1, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCapExcessivePageSize_ToHundred(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListOpenReportsQuery(1, 5000), cancellationToken);

		await _readRepo.Received(1).GetOpenPagedAsync(1, 100, cancellationToken);
	}
}
