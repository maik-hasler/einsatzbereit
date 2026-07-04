using Application.Common.Keycloak;
using Application.Organizations.RemoveMember.v1;
using AwesomeAssertions;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;
using NSubstitute.ExceptionExtensions;


namespace Application.UnitTests.Organizations.RemoveMember;

public class RemoveMemberCommandHandlerTests
{
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly RemoveMemberCommandHandler _sut;

	private static readonly UserId DefaultRequestingUserId = new(Guid.CreateVersion7());

	public RemoveMemberCommandHandlerTests()
	{
		_sut = new RemoveMemberCommandHandler(_keycloakService);
	}

	private void AllowRequestingUserInOrg(Guid orgId) =>
		_keycloakService
			.GetUserOrganizationsAsync(DefaultRequestingUserId.Value, Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganization(orgId, "Test Org")]);

	[Test]
	public async Task Handle_ShouldCallRemoveMemberOnKeycloak(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		AllowRequestingUserInOrg(orgId);
		var command = new RemoveMemberCommand(orgId, userId, DefaultRequestingUserId);

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
		var orgId = Guid.NewGuid();
		AllowRequestingUserInOrg(orgId);
		var command = new RemoveMemberCommand(orgId, Guid.NewGuid(), DefaultRequestingUserId);

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
		AllowRequestingUserInOrg(orgId);
		var command = new RemoveMemberCommand(orgId, userId, DefaultRequestingUserId);

		_keycloakService
			.RemoveMemberAsync(orgId, userId, cancellationToken)
			.ThrowsAsync(new HttpRequestException("Keycloak responded with 404 NotFound"));

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<HttpRequestException>()
			.WithMessage("*404*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotAMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		_keycloakService
			.GetUserOrganizationsAsync(DefaultRequestingUserId.Value, Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganization(Guid.NewGuid(), "Unrelated Org")]);
		var command = new RemoveMemberCommand(orgId, userId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>();
		await _keycloakService.DidNotReceive().RemoveMemberAsync(orgId, userId, Arg.Any<CancellationToken>());
	}
}
