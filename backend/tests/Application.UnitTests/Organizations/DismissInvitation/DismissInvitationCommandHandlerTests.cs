using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Organizations.DismissInvitation.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Users;
using NSubstitute;


namespace Application.UnitTests.Organizations.DismissInvitation;

public class DismissInvitationCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IAggregateRepository<OrganizationInvitation, OrganizationInvitationId> _invitationRepo =
		Substitute.For<IAggregateRepository<OrganizationInvitation, OrganizationInvitationId>>();
	private readonly DismissInvitationCommandHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public DismissInvitationCommandHandlerTests()
	{
		_dbContext.OrganizationInvitations.Returns(_invitationRepo);
		_dbContext
			.IsOrganizerAsync(DefaultOrgId, DefaultRequestingUserId, Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new DismissInvitationCommandHandler(_dbContext, _unitOfWork);
	}

	private static OrganizationInvitation CreateDeclinedInvitation(OrganizationId orgId)
	{
		var invitation = OrganizationInvitation.Create(
			orgId, UserId.New(), UserId.New(), OrganizationMemberRole.Organizer, DateTimeOffset.UtcNow);
		invitation.Decline();
		return invitation;
	}

	private static OrganizationInvitation CreateExpiredInvitation(OrganizationId orgId)
	{
		var now = DateTimeOffset.UtcNow;
		var invitation = OrganizationInvitation.Create(
			orgId, UserId.New(), UserId.New(), OrganizationMemberRole.Organizer, now);
		invitation.Expire(now.AddDays(OrganizationInvitation.ExpiryWindowDays));
		return invitation;
	}

	private static OrganizationInvitation CreatePendingInvitation(OrganizationId orgId) =>
		OrganizationInvitation.Create(
			orgId, UserId.New(), UserId.New(), OrganizationMemberRole.Organizer, DateTimeOffset.UtcNow);

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		// Arrange
		_dbContext
			.IsOrganizerAsync(DefaultOrgId, DefaultRequestingUserId, cancellationToken)
			.Returns(false);
		var invitation = CreateDeclinedInvitation(DefaultOrgId);
		var command = new DismissInvitationCommand(DefaultOrgId, invitation.Id, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*permission*");
		await _invitationRepo.DidNotReceive().FindAsync(Arg.Any<OrganizationInvitationId>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldDeleteInvitation_WhenRequestingUserIsOrgMemberAndInvitationIsDeclined(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreateDeclinedInvitation(DefaultOrgId);
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var command = new DismissInvitationCommand(DefaultOrgId, invitation.Id, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		_invitationRepo.Received(1).Delete(invitation);
		await _dbContext.Received(1).DeleteInvitationReceivedNotificationsAsync(invitation.Id.Value, cancellationToken);
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldDeleteInvitation_WhenRequestingUserIsOrgMemberAndInvitationIsExpired(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreateExpiredInvitation(DefaultOrgId);
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var command = new DismissInvitationCommand(DefaultOrgId, invitation.Id, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		_invitationRepo.Received(1).Delete(invitation);
		await _dbContext.Received(1).DeleteInvitationReceivedNotificationsAsync(invitation.Id.Value, cancellationToken);
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldDeleteInvitation_WhenRequestingUserIsOrgMemberAndInvitationIsPending(
		CancellationToken cancellationToken)
	{
		// #1040: a pending invitation must be revocable, not just Declined/Expired
		// ones - previously an organizer had no way to undo a wrong invite before
		// the invitee acted on it.
		// Arrange
		var invitation = CreatePendingInvitation(DefaultOrgId);
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var command = new DismissInvitationCommand(DefaultOrgId, invitation.Id, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		_invitationRepo.Received(1).Delete(invitation);
		await _dbContext.Received(1).DeleteInvitationReceivedNotificationsAsync(invitation.Id.Value, cancellationToken);
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrowConflict_WhenInvitationIsAlreadyAccepted(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreatePendingInvitation(DefaultOrgId);
		invitation.Accept();
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var command = new DismissInvitationCommand(DefaultOrgId, invitation.Id, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Accepted invitations*");
		_invitationRepo.DidNotReceive().Delete(Arg.Any<OrganizationInvitation>());
		await _dbContext.DidNotReceive().DeleteInvitationReceivedNotificationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
		await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}
}
