using Application.Common.Pagination;
using Application.VolunteerOpportunities;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using AwesomeAssertions;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.GetVolunteerOpportunities;

public class GetVolunteerOpportunitiesQueryHandlerTests
{
	private readonly IVolunteerOpportunityReadRepository _readRepo =
		Substitute.For<IVolunteerOpportunityReadRepository>();
	private readonly GetVolunteerOpportunitiesQueryHandler _sut;

	public GetVolunteerOpportunitiesQueryHandlerTests()
	{
		_readRepo
			.GetPagedSummariesAsync(Arg.Any<VolunteerOpportunityFilter>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<VolunteerOpportunitySummary>([], 0, 1, 10));
		_sut = new GetVolunteerOpportunitiesQueryHandler(_readRepo);
	}

	private static GetVolunteerOpportunitiesQuery Query(int pageNumber, int pageSize) =>
		new(pageNumber, pageSize, null, null, null, null, null, null, null, null, null, null, null, null, null);

	private async Task<VolunteerOpportunityFilter> CapturedFilterAsync(int pageNumber, int pageSize)
	{
		VolunteerOpportunityFilter? captured = null;
		_readRepo
			.GetPagedSummariesAsync(Arg.Do<VolunteerOpportunityFilter>(f => captured = f), Arg.Any<CancellationToken>())
			.Returns(new PagedList<VolunteerOpportunitySummary>([], 0, 1, 10));

		await _sut.Handle(Query(pageNumber, pageSize), CancellationToken.None);

		captured.Should().NotBeNull();
		return captured!;
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageNumber_ToOne()
	{
		var filter = await CapturedFilterAsync(pageNumber: 0, pageSize: 10);
		filter.PageNumber.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldClampNegativePageNumber_ToOne()
	{
		var filter = await CapturedFilterAsync(pageNumber: -5, pageSize: 10);
		filter.PageNumber.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldPreservePositivePageNumber()
	{
		var filter = await CapturedFilterAsync(pageNumber: 3, pageSize: 10);
		filter.PageNumber.Should().Be(3);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageSize_ToOne()
	{
		var filter = await CapturedFilterAsync(pageNumber: 1, pageSize: 0);
		filter.PageSize.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldCapExcessivePageSize_ToHundred()
	{
		var filter = await CapturedFilterAsync(pageNumber: 1, pageSize: 5000);
		filter.PageSize.Should().Be(100);
	}

	[Test]
	public async Task Handle_ShouldPreservePageSizeWithinBounds()
	{
		var filter = await CapturedFilterAsync(pageNumber: 1, pageSize: 25);
		filter.PageSize.Should().Be(25);
	}
}
