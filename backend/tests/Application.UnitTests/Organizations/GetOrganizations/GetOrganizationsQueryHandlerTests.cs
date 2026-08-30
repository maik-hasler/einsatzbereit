using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Organizations;
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
	public async Task Handle_ShouldReturnMemberOrganizations_WithNameAndLogoUrl(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = Guid.NewGuid();
		var org = Organization.Create(OrganizationId.New(), "Fire Department").GetValueOrThrow();
		org.SetLogoUrl("https://example.com/logo.png");

		_dbContext
			.GetMemberOrganizationsAsync(UserId.Create(userId).GetValueOrThrow(), cancellationToken)
			.Returns([new MemberOrganization(org, OrganizationMemberRole.Organizer)]);

		// Act
		var result = await _sut.Handle(new GetOrganizationsQuery(userId), cancellationToken);

		// Assert
		result.Should().HaveCount(1);
		result[0].Id.Should().Be(org.Id.Value);
		result[0].Name.Should().Be("Fire Department");
		result[0].LogoUrl.Should().Be("https://example.com/logo.png");
	}

	// The switcher and the org app's tab bar both hide organizer-only sections
	// from a plain member (#2316), so the role has to travel with every
	// organization in this list, not just with the one currently open.
	[Test]
	[Arguments(OrganizationMemberRole.Member, "Member")]
	[Arguments(OrganizationMemberRole.Organizer, "Organizer")]
	public async Task Handle_ShouldReturnTheRequestingUsersRole_ForEachOrganization(
		OrganizationMemberRole role,
		string expectedRole,
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = Guid.NewGuid();
		var org = Organization.Create(OrganizationId.New(), "Fire Department").GetValueOrThrow();

		_dbContext
			.GetMemberOrganizationsAsync(UserId.Create(userId).GetValueOrThrow(), cancellationToken)
			.Returns([new MemberOrganization(org, role)]);

		// Act
		var result = await _sut.Handle(new GetOrganizationsQuery(userId), cancellationToken);

		// Assert
		result.Should().ContainSingle().Which.Role.Should().Be(expectedRole);
	}

	[Test]
	public async Task Handle_ShouldReturnEmptyList_WhenUserBelongsToNoOrganization(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = Guid.NewGuid();

		_dbContext
			.GetMemberOrganizationsAsync(UserId.Create(userId).GetValueOrThrow(), cancellationToken)
			.Returns([]);

		// Act
		var result = await _sut.Handle(new GetOrganizationsQuery(userId), cancellationToken);

		// Assert
		result.Should().BeEmpty();
	}
}
