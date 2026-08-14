using Application.Common.Pagination;
using Application.Organizations;
using Application.Organizations.GetPublicOrganizations.v1;
using AwesomeAssertions;
using NSubstitute;

namespace Application.UnitTests.Organizations.GetPublicOrganizations;

public class GetPublicOrganizationsQueryHandlerTests
{
	private readonly IOrganizationReadRepository _readRepo =
		Substitute.For<IOrganizationReadRepository>();
	private readonly GetPublicOrganizationsQueryHandler _sut;

	public GetPublicOrganizationsQueryHandlerTests()
	{
		_readRepo
			.GetPagedPublicSummariesAsync(Arg.Any<OrganizationFilter>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<PublicOrganizationSummary>([], 0, 1, 10));
		_sut = new GetPublicOrganizationsQueryHandler(_readRepo);
	}

	private static GetPublicOrganizationsQuery Query(int pageNumber, int pageSize, string? search = null) =>
		new(pageNumber, pageSize, search);

	private async Task<OrganizationFilter> CapturedFilterAsync(int pageNumber, int pageSize, string? search = null)
	{
		OrganizationFilter? captured = null;
		_readRepo
			.GetPagedPublicSummariesAsync(Arg.Do<OrganizationFilter>(f => captured = f), Arg.Any<CancellationToken>())
			.Returns(new PagedList<PublicOrganizationSummary>([], 0, 1, 10));

		await _sut.Handle(Query(pageNumber, pageSize, search), CancellationToken.None);

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

	[Test]
	public async Task Handle_ShouldPassSearchTerm_ToFilter()
	{
		var filter = await CapturedFilterAsync(pageNumber: 1, pageSize: 10, search: "Red Cross");
		filter.Search.Should().Be("Red Cross");
	}
}
