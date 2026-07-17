using Application.Common.Persistence;
using Application.Organizations.GetOrganizations.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Organizations.GetOrganizations;

public class GetOrganizationsQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly GetOrganizationsQueryHandler _sut;

	public GetOrganizationsQueryHandlerTests()
	{
		_sut = new GetOrganizationsQueryHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldReturnOrganizerOrganizations_WithNameAndSlug(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = Guid.NewGuid();
		var org = Organization.Create(new OrganizationId(Guid.NewGuid()), "Fire Department", "fire-department");

		_dbContext
			.GetOrganizerOrganizationsAsync(new UserId(userId), cancellationToken)
			.Returns([org]);

		// Act
		var result = await _sut.Handle(new GetOrganizationsQuery(userId), cancellationToken);

		// Assert
		result.Should().HaveCount(1);
		result[0].Id.Should().Be(org.Id.Value);
		result[0].Name.Should().Be("Fire Department");
		result[0].Slug.Should().Be("fire-department");
	}

	[Test]
	public async Task Handle_ShouldReturnEmptyList_WhenUserOrganizesNothing(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = Guid.NewGuid();

		_dbContext
			.GetOrganizerOrganizationsAsync(new UserId(userId), cancellationToken)
			.Returns([]);

		// Act
		var result = await _sut.Handle(new GetOrganizationsQuery(userId), cancellationToken);

		// Assert
		result.Should().BeEmpty();
	}
}
