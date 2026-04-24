using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Organizations.GetOrganizationDetails.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using NSubstitute;


namespace Application.UnitTests.Organizations.GetOrganizationDetails;

public class GetOrganizationDetailsQueryHandlerTests
{
    private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
    private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
    private readonly IAggregateRepository<Organization, OrganizationId> _orgRepo =
        Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
    private readonly GetOrganizationDetailsQueryHandler _sut;

    public GetOrganizationDetailsQueryHandlerTests()
    {
        _dbContext.Organizations.Returns(_orgRepo);
        _sut = new GetOrganizationDetailsQueryHandler(_dbContext, _keycloakService);
    }

    [Test]
    public async Task Handle_ShouldReturnNull_WhenOrganizationNotFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var orgId = Guid.NewGuid();

        _orgRepo.FindAsync(new OrganizationId(orgId), ct).Returns((Organization?)null);

        // Act
        var result = await _sut.Handle(new GetOrganizationDetailsQuery(orgId), ct);

        // Assert
        result.Should().BeNull();
        await _keycloakService.DidNotReceive().GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReturnOrganizationDetails_WithMembers()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var org = Organization.Create(new OrganizationId(orgId), "Feuerwehr Musterstadt");

        _orgRepo.FindAsync(new OrganizationId(orgId), ct).Returns(org);
        _keycloakService.GetMembersAsync(orgId, ct).Returns([
            new KeycloakOrganizationMember(userId, "olaf", "Olaf", "Müller", "olaf@test.de", true)
        ]);

        // Act
        var result = await _sut.Handle(new GetOrganizationDetailsQuery(orgId), ct);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(orgId);
        result.Name.Should().Be("Feuerwehr Musterstadt");
        result.Members.Should().HaveCount(1);
        result.Members[0].UserId.Should().Be(userId);
        result.Members[0].IsOrganisator.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldMapAddress_WhenAddressIsPresent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var orgId = Guid.NewGuid();
        var org = Organization.Create(new OrganizationId(orgId), "Org");
        org.Update("Org", null, null, null, null,
            new Address("Hauptstraße", "1", "12345", "Berlin"));

        _orgRepo.FindAsync(new OrganizationId(orgId), ct).Returns(org);
        _keycloakService.GetMembersAsync(orgId, ct).Returns([]);

        // Act
        var result = await _sut.Handle(new GetOrganizationDetailsQuery(orgId), ct);

        // Assert
        result!.Address.Should().NotBeNull();
        result.Address!.Street.Should().Be("Hauptstraße");
        result.Address.City.Should().Be("Berlin");
    }

    [Test]
    public async Task Handle_ShouldReturnNullAddress_WhenNoAddressSet()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var orgId = Guid.NewGuid();
        var org = Organization.Create(new OrganizationId(orgId), "Org");

        _orgRepo.FindAsync(new OrganizationId(orgId), ct).Returns(org);
        _keycloakService.GetMembersAsync(orgId, ct).Returns([]);

        // Act
        var result = await _sut.Handle(new GetOrganizationDetailsQuery(orgId), ct);

        // Assert
        result!.Address.Should().BeNull();
    }
}
