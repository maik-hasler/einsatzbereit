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
			.GetFlaggedTargetsPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<FlaggedTargetSummary>([], 0, 1, 10));
		_sut = new ListFlaggedTargetsQueryHandler(_readRepo);
	}

	[Test]
	public async Task Handle_ShouldReturnFlaggedTargets_FromReadRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var item = new FlaggedTargetSummary("VolunteerOpportunity", Guid.NewGuid(), "Titel", "Title", 1, 2, DateTimeOffset.UtcNow, false);

		_readRepo
			.GetFlaggedTargetsPagedAsync(1, 10, false, cancellationToken)
			.Returns(new PagedList<FlaggedTargetSummary>([item], 1, 1, 10));

		// Act
		var result = await _sut.Handle(new ListFlaggedTargetsQuery(1, 10, IncludeResolved: false), cancellationToken);

		// Assert
		result.Items.Should().ContainSingle().Which.Should().Be(item);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListFlaggedTargetsQuery(0, 10, IncludeResolved: false), cancellationToken);

		await _readRepo.Received(1).GetFlaggedTargetsPagedAsync(1, 10, false, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampNegativePageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListFlaggedTargetsQuery(-5, 10, IncludeResolved: false), cancellationToken);

		await _readRepo.Received(1).GetFlaggedTargetsPagedAsync(1, 10, false, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageSize_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListFlaggedTargetsQuery(1, 0, IncludeResolved: false), cancellationToken);

		await _readRepo.Received(1).GetFlaggedTargetsPagedAsync(1, 1, false, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCapExcessivePageSize_ToHundred(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListFlaggedTargetsQuery(1, 5000, IncludeResolved: false), cancellationToken);

		await _readRepo.Received(1).GetFlaggedTargetsPagedAsync(1, 100, false, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldForwardIncludeResolved_ToReadRepository(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListFlaggedTargetsQuery(1, 10, IncludeResolved: true), cancellationToken);

		await _readRepo.Received(1).GetFlaggedTargetsPagedAsync(1, 10, true, cancellationToken);
	}
}
