using Application.Abstractions;
using Application.Organisationen.CreateOrganisation.v1;
using AwesomeAssertions;
using Domain.Organisationen;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Application.UnitTests.Organisationen.CreateOrganisation;

public class CreateOrganisationCommandHandlerTests
{
    private readonly IKeycloakOrganisationService _keycloakService = Substitute.For<IKeycloakOrganisationService>();
    private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateOrganisationCommandHandler _sut;

    public CreateOrganisationCommandHandlerTests()
    {
        _sut = new CreateOrganisationCommandHandler(
            _keycloakService,
            _dbContext,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldCreateOrganisationInKeycloakAndDatabase()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganisationCommand("Feuerwehr Musterstadt", userId);

        _keycloakService
            .CreateOrganisationAsync("Feuerwehr Musterstadt", ct)
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
        var command = new CreateOrganisationCommand("Test Org", userId);

        _keycloakService
            .CreateOrganisationAsync("Test Org", ct)
            .Returns(keycloakId);

        // Act
        await _sut.Handle(command, ct);

        // Assert
        await _keycloakService.Received(1).AddMemberAsync(keycloakId, userId, ct);
    }

    [Fact]
    public async Task Handle_ShouldAssignOrganisatorRoleToCreator()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganisationCommand("Test Org", userId);

        _keycloakService
            .CreateOrganisationAsync("Test Org", ct)
            .Returns(keycloakId);

        // Act
        await _sut.Handle(command, ct);

        // Assert
        await _keycloakService.Received(1).AssignOrganisatorRoleAsync(userId, ct);
    }

    [Fact]
    public async Task Handle_ShouldPersistOrganisationToRepository()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganisationCommand("Test Org", userId);

        _keycloakService
            .CreateOrganisationAsync("Test Org", ct)
            .Returns(keycloakId);

        // Act
        await _sut.Handle(command, ct);

        // Assert
        await _dbContext.Organisationen.Received(1).AddAsync(
            Arg.Is<Organisation>(o => o.Name == "Test Org"),
            ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }

    [Fact]
    public async Task Handle_ShouldCallKeycloakOperationsInCorrectOrder()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganisationCommand("Test Org", userId);
        var callOrder = new List<string>();

        _keycloakService
            .CreateOrganisationAsync("Test Org", ct)
            .Returns(ci =>
            {
                callOrder.Add("CreateOrganisation");
                return keycloakId;
            });

        _keycloakService
            .When(x => x.AddMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), ct))
            .Do(_ => callOrder.Add("AddMember"));

        _keycloakService
            .When(x => x.AssignOrganisatorRoleAsync(Arg.Any<Guid>(), ct))
            .Do(_ => callOrder.Add("AssignRole"));

        _unitOfWork
            .When(x => x.SaveChangesAsync(ct))
            .Do(_ => callOrder.Add("SaveChanges"));

        // Act
        await _sut.Handle(command, ct);

        // Assert
        callOrder.Should().Equal(
            "CreateOrganisation", "AddMember", "AssignRole", "SaveChanges");
    }

    [Fact]
    public async Task Handle_ShouldPropagateException_WhenKeycloakCreateFails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var command = new CreateOrganisationCommand("Bad Org", userId);

        _keycloakService
            .CreateOrganisationAsync("Bad Org", ct)
            .ThrowsAsync(new HttpRequestException("Keycloak responded with 400 BadRequest"));

        // Act
        Func<Task> act = async () => await _sut.Handle(command, ct);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        await _dbContext.Organisationen.DidNotReceive().AddAsync(
            Arg.Any<Organisation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPropagateException_WhenAddMemberFails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var keycloakId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateOrganisationCommand("Test Org", userId);

        _keycloakService
            .CreateOrganisationAsync("Test Org", ct)
            .Returns(keycloakId);

        _keycloakService
            .AddMemberAsync(keycloakId, userId, ct)
            .ThrowsAsync(new HttpRequestException("User does not exist"));

        // Act
        Func<Task> act = async () => await _sut.Handle(command, ct);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*User does not exist*");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
