using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Invitations.AcceptInvitation.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Invitations.AcceptInvitation;

public class AcceptInvitationCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<OrganizationInvitation, OrganizationInvitationId> _invitationRepo =
		Substitute.For<IAggregateRepository<OrganizationInvitation, OrganizationInvitationId>>();
	private readonly IAggregateRepository<OrganizationMembership, OrganizationMembershipId> _membershipRepo =
		Substitute.For<IAggregateRepository<OrganizationMembership, OrganizationMembershipId>>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly AcceptInvitationCommandHandler _sut;

	private static readonly OrganizationId OrgId = OrganizationId.Create(Guid.NewGuid()).GetValueOrThrow();
	private static readonly UserId InviteeId = UserId.New();
	private static readonly UserId InviterId = UserId.New();

	public AcceptInvitationCommandHandlerTests()
	{
		_dbContext.OrganizationInvitations.Returns(_invitationRepo);
		_dbContext.OrganizationMemberships.Returns(_membershipRepo);
		_dbContext
			.GetMembershipAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns((OrganizationMembership?)null);
		_sut = new AcceptInvitationCommandHandler(_dbContext, _unitOfWork, _keycloakService);
	}

	private static OrganizationInvitation CreatePendingInvitation(
		OrganizationMemberRole intendedRole = OrganizationMemberRole.Organizer) =>
		OrganizationInvitation.Create(OrgId, InviteeId, InviterId, intendedRole, DateTimeOffset.UtcNow);

	[Test]
	public async Task Handle_ShouldGrantOrganizerCapability_OnAccept(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreatePendingInvitation();
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var command = new AcceptInvitationCommand(invitation.Id, InviteeId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert

		result.Should().BeTrue();
		await _keycloakService.Received(1).AddMemberAsync(OrgId.Value, InviteeId.Value, cancellationToken);
		await _keycloakService.Received(1).AssignOrganizerRoleAsync(InviteeId.Value, cancellationToken);
		await _membershipRepo.Received(1).AddAsync(
			Arg.Is<OrganizationMembership>(m =>
				m != null && m.OrganizationId == OrgId && m.UserId == InviteeId && m.Role == OrganizationMemberRole.Organizer),
			cancellationToken);
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldGrantMemberCapabilityOnly_WhenInvitationIntendedRoleIsMember(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreatePendingInvitation(OrganizationMemberRole.Member);
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var command = new AcceptInvitationCommand(invitation.Id, InviteeId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert

		result.Should().BeTrue();
		await _keycloakService.Received(1).AddMemberAsync(OrgId.Value, InviteeId.Value, cancellationToken);
		await _keycloakService.DidNotReceive().AssignOrganizerRoleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
		await _membershipRepo.Received(1).AddAsync(
			Arg.Is<OrganizationMembership>(m =>
				m != null && m.OrganizationId == OrgId && m.UserId == InviteeId && m.Role == OrganizationMemberRole.Member),
			cancellationToken);
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenInvitationNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitationId = OrganizationInvitationId.New();
		_invitationRepo.FindAsync(invitationId, cancellationToken).Returns((OrganizationInvitation?)null);
		var command = new AcceptInvitationCommand(invitationId, InviteeId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>();
		await _keycloakService.DidNotReceive().AddMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotTheRecipient(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreatePendingInvitation();
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var someoneElse = UserId.New();
		var command = new AcceptInvitationCommand(invitation.Id, someoneElse);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>();
		await _keycloakService.DidNotReceive().AddMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
		await _membershipRepo.DidNotReceive().AddAsync(Arg.Any<OrganizationMembership>(), Arg.Any<CancellationToken>());
		await _dbContext.DidNotReceive().DeleteInvitationReceivedNotificationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenInvitationIsNotPending(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreatePendingInvitation();
		invitation.Accept().ThrowIfFailure();
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var command = new AcceptInvitationCommand(invitation.Id, InviteeId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>();
		await _keycloakService.DidNotReceive().AddMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
		await _membershipRepo.DidNotReceive().AddAsync(Arg.Any<OrganizationMembership>(), Arg.Any<CancellationToken>());
		await _dbContext.DidNotReceive().DeleteInvitationReceivedNotificationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldDeleteInvitationReceivedNotification_OnAccept(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreatePendingInvitation();
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var command = new AcceptInvitationCommand(invitation.Id, InviteeId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _dbContext.Received(1).DeleteInvitationReceivedNotificationsAsync(invitation.Id.Value, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldBeIdempotentNoOp_WhenMembershipAlreadyExists(
		CancellationToken cancellationToken)
	{
		var invitation = CreatePendingInvitation();
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		_dbContext
			.GetMembershipAsync(OrgId, InviteeId, cancellationToken)
			.Returns(OrganizationMembership.Create(OrgId, InviteeId, OrganizationMemberRole.Organizer));
		var command = new AcceptInvitationCommand(invitation.Id, InviteeId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _keycloakService.DidNotReceive().AddMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
		await _membershipRepo.DidNotReceive().AddAsync(Arg.Any<OrganizationMembership>(), Arg.Any<CancellationToken>());
		await _dbContext.DidNotReceive().DeleteInvitationReceivedNotificationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}
}
