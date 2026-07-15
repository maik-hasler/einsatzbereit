using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Organizations.RemoveMember.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;
using NSubstitute.ExceptionExtensions;


namespace Application.UnitTests.Organizations.RemoveMember;

public class RemoveMemberCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly RemoveMemberCommandHandler _sut;

	private static readonly UserId DefaultRequestingUserId = new(Guid.CreateVersion7());

	public RemoveMemberCommandHandlerTests()
	{
		_sut = new RemoveMemberCommandHandler(_dbContext, _keycloakService);
	}

	private void AllowRequestingUserInOrg(Guid orgId) =>
		_dbContext
			.IsOrganizerAsync(new OrganizationId(orgId), DefaultRequestingUserId, Arg.Any<CancellationToken>())
			.Returns(true);

	private void SetMembers(Guid orgId, params KeycloakOrganizationMember[] members) =>
		_keycloakService
			.GetMembersAsync(orgId, Arg.Any<CancellationToken>())
			.Returns((IReadOnlyList<KeycloakOrganizationMember>)members);

	private static KeycloakOrganizationMember Member(Guid userId) =>
		new(userId, "user", "First", "Last", "user@example.com", IsOrganisator: false);

	[Test]
	public async Task Handle_ShouldCallRemoveMemberOnKeycloak(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		AllowRequestingUserInOrg(orgId);
		SetMembers(orgId, Member(DefaultRequestingUserId.Value), Member(userId));
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
		var userId = Guid.NewGuid();
		AllowRequestingUserInOrg(orgId);
		SetMembers(orgId, Member(DefaultRequestingUserId.Value), Member(userId));
		var command = new RemoveMemberCommand(orgId, userId, DefaultRequestingUserId);

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
		SetMembers(orgId, Member(DefaultRequestingUserId.Value), Member(userId));
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
		_dbContext
			.IsOrganizerAsync(new OrganizationId(orgId), DefaultRequestingUserId, Arg.Any<CancellationToken>())
			.Returns(false);
		var command = new RemoveMemberCommand(orgId, userId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>();
		await _keycloakService.DidNotReceive().RemoveMemberAsync(orgId, userId, Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRemovingTheLastRemainingMember(
		CancellationToken cancellationToken)
	{
		// Arrange - the requesting user is the org's sole member removing (leaving) themselves.
		var orgId = Guid.NewGuid();
		AllowRequestingUserInOrg(orgId);
		SetMembers(orgId, Member(DefaultRequestingUserId.Value));
		var command = new RemoveMemberCommand(orgId, DefaultRequestingUserId.Value, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage("*only member*");
		await _keycloakService.DidNotReceive().RemoveMemberAsync(orgId, DefaultRequestingUserId.Value, Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldAllowRemoval_WhenMultipleMembersRemain(
		CancellationToken cancellationToken)
	{
		// Arrange - two members left, removing one is fine.
		var orgId = Guid.NewGuid();
		var otherUserId = Guid.NewGuid();
		AllowRequestingUserInOrg(orgId);
		SetMembers(orgId, Member(DefaultRequestingUserId.Value), Member(otherUserId));
		var command = new RemoveMemberCommand(orgId, otherUserId, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _keycloakService.Received(1).RemoveMemberAsync(orgId, otherUserId, cancellationToken);
	}
}
