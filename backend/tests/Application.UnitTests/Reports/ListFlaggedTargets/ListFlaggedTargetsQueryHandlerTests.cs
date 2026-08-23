using Application.Common.Pagination;
using Application.Reports;
using Application.Reports.ListFlaggedTargets.v1;
using AwesomeAssertions;
using NSubstitute;

namespace Application.UnitTests.Reports.ListFlaggedTargets;

public class ListFlaggedTargetsQueryHandlerTests
{
	private readonly IAdminReportReadRepository _readRepo =
		Substitute.For<IAdminReportReadRepository>();
	private readonly ListFlaggedTargetsQueryHandler _sut;

	public ListFlaggedTargetsQueryHandlerTests()
	{
		_readRepo
			.GetFlaggedTargetsPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<FlaggedTargetSummary>([], 0, 1, 10));
		_sut = new ListFlaggedTargetsQueryHandler(_readRepo);
	}

	[Test]
	public async Task Handle_ShouldReturnFlaggedTargets_FromReadRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var item = new FlaggedTargetSummary("VolunteerOpportunity", Guid.NewGuid(), "Titel", 1, 2, DateTimeOffset.UtcNow, false);

		_readRepo
			.GetFlaggedTargetsPagedAsync(1, 10, cancellationToken)
			.Returns(new PagedList<FlaggedTargetSummary>([item], 1, 1, 10));

		// Act
		var result = await _sut.Handle(new ListFlaggedTargetsQuery(1, 10), cancellationToken);

		// Assert
		result.Items.Should().ContainSingle().Which.Should().Be(item);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListFlaggedTargetsQuery(0, 10), cancellationToken);

		await _readRepo.Received(1).GetFlaggedTargetsPagedAsync(1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampNegativePageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListFlaggedTargetsQuery(-5, 10), cancellationToken);

		await _readRepo.Received(1).GetFlaggedTargetsPagedAsync(1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageSize_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListFlaggedTargetsQuery(1, 0), cancellationToken);

		await _readRepo.Received(1).GetFlaggedTargetsPagedAsync(1, 1, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCapExcessivePageSize_ToHundred(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListFlaggedTargetsQuery(1, 5000), cancellationToken);

		await _readRepo.Received(1).GetFlaggedTargetsPagedAsync(1, 100, cancellationToken);
	}
}
