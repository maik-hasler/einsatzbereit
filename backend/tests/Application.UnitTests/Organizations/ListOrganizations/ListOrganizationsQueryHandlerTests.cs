using Application.Common.Pagination;
using Application.Organizations;
using Application.Organizations.ListOrganizations.v1;
using AwesomeAssertions;
using NSubstitute;

namespace Application.UnitTests.Organizations.ListOrganizations;

public class ListOrganizationsQueryHandlerTests
{
	private readonly IAdminOrganizationReadRepository _readRepo =
		Substitute.For<IAdminOrganizationReadRepository>();
	private readonly ListOrganizationsQueryHandler _sut;

	public ListOrganizationsQueryHandlerTests()
	{
		_readRepo
			.GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<AdminOrganizationSummary>([], 0, 1, 10));
		_sut = new ListOrganizationsQueryHandler(_readRepo);
	}

	[Test]
	public async Task Handle_ShouldReturnOrganizations_FromReadRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var item = new AdminOrganizationSummary(Guid.NewGuid(), "Fire Department", null);

		_readRepo
			.GetPagedAsync(1, 10, cancellationToken)
			.Returns(new PagedList<AdminOrganizationSummary>([item], 1, 1, 10));

		// Act
		var result = await _sut.Handle(new ListOrganizationsQuery(1, 10), cancellationToken);

		// Assert
		result.Items.Should().ContainSingle().Which.Should().Be(item);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListOrganizationsQuery(0, 10), cancellationToken);

		await _readRepo.Received(1).GetPagedAsync(1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampNegativePageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListOrganizationsQuery(-5, 10), cancellationToken);

		await _readRepo.Received(1).GetPagedAsync(1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageSize_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListOrganizationsQuery(1, 0), cancellationToken);

		await _readRepo.Received(1).GetPagedAsync(1, 1, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCapExcessivePageSize_ToHundred(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListOrganizationsQuery(1, 5000), cancellationToken);

		await _readRepo.Received(1).GetPagedAsync(1, 100, cancellationToken);
	}
}
