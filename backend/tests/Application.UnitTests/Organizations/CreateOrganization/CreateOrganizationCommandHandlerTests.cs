using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Organizations.CreateOrganization.v1;
using AwesomeAssertions;
using Domain.Organizations;
using NSubstitute;
using NSubstitute.ExceptionExtensions;


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

    [Test]
    public async Task Handle_ShouldCreateOrganizationInKeycloakAndDatabase(
        CancellationToken cancellationToken)
    {
        // Arrange
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Feuerwehr Musterstadt", userId);

        _keycloakService
            .CreateOrganizationAsync("Feuerwehr Musterstadt", cancellationToken)
            .Returns(keycloakId);

        // Act
        var result = await _sut.Handle(command, cancellationToken);

        // Assert
        result.Name.Should().Be("Feuerwehr Musterstadt");
    }

    [Test]
    public async Task Handle_ShouldAddCreatorAsMemberInKeycloak(
        CancellationToken cancellationToken)
    {
        // Arrange
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Test Org", userId);

        _keycloakService
            .CreateOrganizationAsync("Test Org",cancellationToken)
            .Returns(keycloakId);

        // Act
        await _sut.Handle(command,cancellationToken);

        // Assert
        await _keycloakService.Received(1).AddMemberAsync(keycloakId, userId,cancellationToken);
    }

    [Test]
    public async Task Handle_ShouldAssignOrganizerRoleToCreator(
        CancellationToken cancellationToken)
    {
        // Arrange
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Test Org", userId);

        _keycloakService
            .CreateOrganizationAsync("Test Org",cancellationToken)
            .Returns(keycloakId);

        // Act
        await _sut.Handle(command,cancellationToken);

        // Assert
        await _keycloakService.Received(1).AssignOrganizerRoleAsync(userId,cancellationToken);
    }

    [Test]
    public async Task Handle_ShouldPersistOrganizationToRepository(
        CancellationToken cancellationToken)
    {
        // Arrange
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Test Org", userId);

        _keycloakService
            .CreateOrganizationAsync("Test Org",cancellationToken)
            .Returns(keycloakId);

        // Act
        await _sut.Handle(command,cancellationToken);

        // Assert
        await _dbContext.Organizations.Received(1).AddAsync(
            Arg.Is<Organization>(o => o.Name == "Test Org"),
           cancellationToken);
    }

    [Test]
    public async Task Handle_ShouldCallKeycloakOperationsInCorrectOrder(
        CancellationToken cancellationToken)
    {
        // Arrange
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Test Org", userId);
        var callOrder = new List<string>();

        _keycloakService
            .CreateOrganizationAsync("Test Org",cancellationToken)
            .Returns(_ =>
            {
                callOrder.Add("CreateOrganization");
                return keycloakId;
            });

        _keycloakService
            .When(x => x.AddMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),cancellationToken))
            .Do(_ => callOrder.Add("AddMember"));

        _keycloakService
            .When(x => x.AssignOrganizerRoleAsync(Arg.Any<Guid>(),cancellationToken))
            .Do(_ => callOrder.Add("AssignRole"));

        // Act
        await _sut.Handle(command,cancellationToken);

        // Assert
        callOrder.Should().Equal(
            "CreateOrganization", "AddMember", "AssignRole");
    }

    [Test]
    public async Task Handle_ShouldPropagateException_WhenKeycloakCreateFails(
        CancellationToken cancellationToken)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Bad Org", userId);

        _keycloakService
            .CreateOrganizationAsync("Bad Org",cancellationToken)
            .ThrowsAsync(new HttpRequestException("Keycloak responded with 400 BadRequest"));

        // Act
        Func<Task> act = async () => await _sut.Handle(command,cancellationToken);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        await _dbContext.Organizations.DidNotReceive().AddAsync(
            Arg.Any<Organization>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldPropagateException_WhenAddMemberFails(
        CancellationToken cancellationToken)
    {
        // Arrange
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganizationCommand("Test Org", userId);

        _keycloakService
            .CreateOrganizationAsync("Test Org",cancellationToken)
            .Returns(keycloakId);

        _keycloakService
            .AddMemberAsync(keycloakId, userId,cancellationToken)
            .ThrowsAsync(new HttpRequestException("User does not exist"));

        // Act
        Func<Task> act = async () => await _sut.Handle(command,cancellationToken);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*User does not exist*");
    }
}
