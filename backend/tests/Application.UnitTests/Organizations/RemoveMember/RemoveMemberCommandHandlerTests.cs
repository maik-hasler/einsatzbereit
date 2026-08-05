using Application.Common.Exceptions;
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

	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public RemoveMemberCommandHandlerTests()
	{
		_sut = new RemoveMemberCommandHandler(_dbContext, _keycloakService);
	}

	private void AllowRequestingUserInOrg(Guid orgId)
	{
		var organizationId = OrganizationId.Create(orgId).GetValueOrThrow();
		_dbContext.IsOrganizerAsync(organizationId, DefaultRequestingUserId, Arg.Any<CancellationToken>()).Returns(true);
		_dbContext.IsMemberAsync(organizationId, DefaultRequestingUserId, Arg.Any<CancellationToken>()).Returns(true);
	}

	private void AllowRequestingUserAsPlainMember(Guid orgId)
	{
		var organizationId = OrganizationId.Create(orgId).GetValueOrThrow();
		_dbContext.IsOrganizerAsync(organizationId, DefaultRequestingUserId, Arg.Any<CancellationToken>()).Returns(false);
		_dbContext.IsMemberAsync(organizationId, DefaultRequestingUserId, Arg.Any<CancellationToken>()).Returns(true);
	}

	private void SetTargetIsOrganizer(Guid orgId, Guid targetUserId, bool isOrganizer) =>
		_dbContext
			.IsOrganizerAsync(OrganizationId.Create(orgId).GetValueOrThrow(), UserId.Create(targetUserId).GetValueOrThrow(), Arg.Any<CancellationToken>())
			.Returns(isOrganizer);

	private void SetOrganizerCount(Guid orgId, int count) =>
		_dbContext
			.CountOrganizersAsync(OrganizationId.Create(orgId).GetValueOrThrow(), Arg.Any<CancellationToken>())
			.Returns(count);

	[Test]
	public async Task Handle_ShouldCallRemoveMemberOnKeycloak(
		CancellationToken cancellationToken)
	{
		// Arrange - target is a regular (non-organizer) member.
		var orgId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		AllowRequestingUserInOrg(orgId);
		SetTargetIsOrganizer(orgId, userId, isOrganizer: false);
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
		SetTargetIsOrganizer(orgId, userId, isOrganizer: false);
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
		SetTargetIsOrganizer(orgId, userId, isOrganizer: false);
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
			.IsOrganizerAsync(OrganizationId.Create(orgId).GetValueOrThrow(), DefaultRequestingUserId, Arg.Any<CancellationToken>())
			.Returns(false);
		var command = new RemoveMemberCommand(orgId, userId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>();
		await _keycloakService.DidNotReceive().RemoveMemberAsync(orgId, userId, Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRemovingTheLastRemainingOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange - the requesting user is the org's sole organizer, removing (leaving)
		// themselves, even though the org may have other, non-organizer members (e.g. an
		// accepted-but-never-promoted invitee). Only the organizer count, never the total
		// headcount, may gate this, or a sole organizer could leave and orphan the org.
		var orgId = Guid.NewGuid();
		AllowRequestingUserInOrg(orgId);
		SetOrganizerCount(orgId, 1);
		var command = new RemoveMemberCommand(orgId, DefaultRequestingUserId.Value, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*only organizer*");
		await _keycloakService.DidNotReceive().RemoveMemberAsync(orgId, DefaultRequestingUserId.Value, Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldAllowRemoval_WhenTargetIsNotAnOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange - removing a non-organizer member never triggers the guard, regardless
		// of how many organizers the org has.
		var orgId = Guid.NewGuid();
		var otherUserId = Guid.NewGuid();
		AllowRequestingUserInOrg(orgId);
		SetTargetIsOrganizer(orgId, otherUserId, isOrganizer: false);
		var command = new RemoveMemberCommand(orgId, otherUserId, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _keycloakService.Received(1).RemoveMemberAsync(orgId, otherUserId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldAllowSelfRemoval_WhenRequestingUserIsOnlyAPlainMember(
		CancellationToken cancellationToken)
	{
		// Arrange - a plain (non-organizer) Member leaving the organization is a
		// self-service action, not org management - it must not require Organizer.
		var orgId = Guid.NewGuid();
		AllowRequestingUserAsPlainMember(orgId);
		SetTargetIsOrganizer(orgId, DefaultRequestingUserId.Value, isOrganizer: false);
		var command = new RemoveMemberCommand(orgId, DefaultRequestingUserId.Value, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _keycloakService.Received(1).RemoveMemberAsync(orgId, DefaultRequestingUserId.Value, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenPlainMemberTriesToRemoveSomeoneElse(
		CancellationToken cancellationToken)
	{
		// Arrange - removing another member is org management and still requires Organizer,
		// even though the requester is a valid Member of the organization.
		var orgId = Guid.NewGuid();
		var otherUserId = Guid.NewGuid();
		AllowRequestingUserAsPlainMember(orgId);
		var command = new RemoveMemberCommand(orgId, otherUserId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>();
		await _keycloakService.DidNotReceive().RemoveMemberAsync(orgId, otherUserId, Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldAllowRemoval_WhenAnotherOrganizerRemains(
		CancellationToken cancellationToken)
	{
		// Arrange - two organizers; one of them leaving is fine because the org still
		// has an organizer afterwards.
		var orgId = Guid.NewGuid();
		AllowRequestingUserInOrg(orgId);
		SetOrganizerCount(orgId, 2);
		var command = new RemoveMemberCommand(orgId, DefaultRequestingUserId.Value, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _keycloakService.Received(1).RemoveMemberAsync(orgId, DefaultRequestingUserId.Value, cancellationToken);
	}
}
