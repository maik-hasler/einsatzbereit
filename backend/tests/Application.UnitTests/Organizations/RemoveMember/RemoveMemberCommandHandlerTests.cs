using Application.Common.Keycloak;
using Application.Organizations.RemoveMember.v1;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;


namespace Application.UnitTests.Organizations.RemoveMember;

public class RemoveMemberCommandHandlerTests
{
    private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
    private readonly RemoveMemberCommandHandler _sut;

    public RemoveMemberCommandHandlerTests()
    {
        _sut = new RemoveMemberCommandHandler(_keycloakService);
    }

    [Test]
    public async Task Handle_ShouldCallRemoveMemberOnKeycloak(
        CancellationToken cancellationToken)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RemoveMemberCommand(orgId, userId);

        // Act
        await _sut.Handle(command, cancellationToken);

        // Assert
        await _keycloakService.Received(1).RemoveMemberAsync(orgId, userId, cancellationToken);
    }

    [Test]
    public async Task Handle_ShouldReturnTrue_OnSuccess(
        CancellationToken cancellationToken)
    {
        // Arrange
        var command = new RemoveMemberCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _sut.Handle(command, cancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldPropagateException_WhenKeycloakFails(
        CancellationToken cancellationToken)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RemoveMemberCommand(orgId, userId);

        _keycloakService
            .RemoveMemberAsync(orgId, userId, cancellationToken)
            .ThrowsAsync(new HttpRequestException("Keycloak responded with 404 NotFound"));

        // Act
        Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*404*");
    }
}
