using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Organizations.GetOrganizationDetails.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Users;
using NSubstitute;


namespace Application.UnitTests.Organizations.GetOrganizationDetails;

public class GetOrganizationDetailsQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly IAggregateRepository<Organization, OrganizationId> _orgRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly GetOrganizationDetailsQueryHandler _sut;

	private static readonly Guid DefaultOrgId = Guid.NewGuid();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public GetOrganizationDetailsQueryHandlerTests()
	{
		_dbContext.Organizations.Returns(_orgRepo);
		_keycloakService
			.GetUserOrganizationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganization(DefaultOrgId, "Test Org")]);
		_sut = new GetOrganizationDetailsQueryHandler(_dbContext, _keycloakService);
	}

	[Test]
	public async Task Handle_ShouldReturnNull_WhenOrganizationNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = DefaultOrgId;

		_orgRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns((Organization?)null);

		// Act
		var result = await _sut.Handle(new GetOrganizationDetailsQuery(orgId, DefaultRequestingUserId), cancellationToken);

		// Assert
		result.Should().BeNull();
		await _keycloakService.DidNotReceive().GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldReturnOrganizationDetails_WithMembers(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = DefaultOrgId;
		var userId = Guid.NewGuid();
		var org = Organization.Create(OrganizationId.Create(orgId).GetValueOrThrow(), "Sample Fire Department").Value;

		_orgRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(org);
		_keycloakService.GetMembersAsync(orgId, cancellationToken).Returns([
			new KeycloakOrganizationMember(userId, "olaf", "Olaf", "Miller", "olaf@test.de", true)
		]);

		// Act
		var result = await _sut.Handle(new GetOrganizationDetailsQuery(orgId, DefaultRequestingUserId), cancellationToken);

		// Assert
		result.Should().NotBeNull();
		result!.Id.Should().Be(orgId);
		result.Name.Should().Be("Sample Fire Department");
		result.Members.Should().HaveCount(1);
		result.Members[0].UserId.Should().Be(userId);
		result.Members[0].IsOrganisator.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldMapAddress_WhenAddressIsPresent(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = DefaultOrgId;
		var org = Organization.Create(OrganizationId.Create(orgId).GetValueOrThrow(), "Org").Value;
		org.Relocate(Address.Create("Main Street", "1", "12345", "Berlin").Value);

		_orgRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(org);
		_keycloakService.GetMembersAsync(orgId, cancellationToken).Returns([]);

		// Act
		var result = await _sut.Handle(new GetOrganizationDetailsQuery(orgId, DefaultRequestingUserId), cancellationToken);

		// Assert
		result!.Address.Should().NotBeNull();
		result.Address!.Street.Should().Be("Main Street");
		result.Address.City.Should().Be("Berlin");
	}

	[Test]
	public async Task Handle_ShouldReturnNullAddress_WhenNoAddressSet(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = DefaultOrgId;
		var org = Organization.Create(OrganizationId.Create(orgId).GetValueOrThrow(), "Org").Value;

		_orgRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(org);
		_keycloakService.GetMembersAsync(orgId, cancellationToken).Returns([]);

		// Act
		var result = await _sut.Handle(new GetOrganizationDetailsQuery(orgId, DefaultRequestingUserId), cancellationToken);

		// Assert
		result!.Address.Should().BeNull();
	}
}
