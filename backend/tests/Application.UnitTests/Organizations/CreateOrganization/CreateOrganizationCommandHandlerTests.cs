using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Organizations.CreateOrganization.v1;
using AwesomeAssertions;
using Domain.Organizations;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Application.UnitTests.Organizations.CreateOrganization;

public class CreateOrganizationCommandHandlerTests
{
    private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
    private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
    private readonly CreateOrganizationCommandHandler _sut;

    public CreateOrganizationCommandHandlerTests()
    {
        _sut = new CreateOrganizationCommandHandler(
            _keycloakService,
            _dbContext);
    }

    [Fact]
    public async Task Handle_ShouldCreateOrganizationInKeycloakAndDatabase()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Feuerwehr Musterstadt", userId);

        _keycloakService
            .CreateOrganizationAsync("Feuerwehr Musterstadt", ct)
            .Returns(keycloakId);

        // Act
        var result = await _sut.Handle(command, ct);

        // Assert
        result.Name.Should().Be("Feuerwehr Musterstadt");
    }

    [Fact]
    public async Task Handle_ShouldAddCreatorAsMemberInKeycloak()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Test Org", userId);

        _keycloakService
            .CreateOrganizationAsync("Test Org", ct)
            .Returns(keycloakId);

        // Act
        await _sut.Handle(command, ct);

        // Assert
        await _keycloakService.Received(1).AddMemberAsync(keycloakId, userId, ct);
    }

    [Fact]
    public async Task Handle_ShouldAssignOrganizerRoleToCreator()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Test Org", userId);

        _keycloakService
            .CreateOrganizationAsync("Test Org", ct)
            .Returns(keycloakId);

        // Act
        await _sut.Handle(command, ct);

        // Assert
        await _keycloakService.Received(1).AssignOrganizerRoleAsync(userId, ct);
    }

    [Fact]
    public async Task Handle_ShouldPersistOrganizationToRepository()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Test Org", userId);

        _keycloakService
            .CreateOrganizationAsync("Test Org", ct)
            .Returns(keycloakId);

        // Act
        await _sut.Handle(command, ct);

        // Assert
        await _dbContext.Organizations.Received(1).AddAsync(
            Arg.Is<Organization>(o => o.Name == "Test Org"),
            ct);
    }

    [Fact]
    public async Task Handle_ShouldCallKeycloakOperationsInCorrectOrder()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Test Org", userId);
        var callOrder = new List<string>();

        _keycloakService
            .CreateOrganizationAsync("Test Org", ct)
            .Returns(ci =>
            {
                callOrder.Add("CreateOrganization");
                return keycloakId;
            });

        _keycloakService
            .When(x => x.AddMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), ct))
            .Do(_ => callOrder.Add("AddMember"));

        _keycloakService
            .When(x => x.AssignOrganizerRoleAsync(Arg.Any<Guid>(), ct))
            .Do(_ => callOrder.Add("AssignRole"));

        // Act
        await _sut.Handle(command, ct);

        // Assert
        callOrder.Should().Equal(
            "CreateOrganization", "AddMember", "AssignRole");
    }

    [Fact]
    public async Task Handle_ShouldPropagateException_WhenKeycloakCreateFails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Bad Org", userId);

        _keycloakService
            .CreateOrganizationAsync("Bad Org", ct)
            .ThrowsAsync(new HttpRequestException("Keycloak responded with 400 BadRequest"));

        // Act
        Func<Task> act = async () => await _sut.Handle(command, ct);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        await _dbContext.Organizations.DidNotReceive().AddAsync(
            Arg.Any<Organization>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPropagateException_WhenAddMemberFails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Test Org", userId);

        _keycloakService
            .CreateOrganizationAsync("Test Org", ct)
            .Returns(keycloakId);

        _keycloakService
            .AddMemberAsync(keycloakId, userId, ct)
            .ThrowsAsync(new HttpRequestException("User does not exist"));

        // Act
        Func<Task> act = async () => await _sut.Handle(command, ct);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*User does not exist*");
    }
}
