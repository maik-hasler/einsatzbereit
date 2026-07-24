using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Invitations.DeclineInvitation.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Invitations.DeclineInvitation;

public class DeclineInvitationCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<OrganizationInvitation, OrganizationInvitationId> _invitationRepo =
		Substitute.For<IAggregateRepository<OrganizationInvitation, OrganizationInvitationId>>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly DeclineInvitationCommandHandler _sut;

	private static readonly OrganizationId OrgId = OrganizationId.Create(Guid.NewGuid()).GetValueOrThrow();
	private static readonly UserId InviteeId = UserId.New();
	private static readonly UserId InviterId = UserId.New();

	public DeclineInvitationCommandHandlerTests()
	{
		_dbContext.OrganizationInvitations.Returns(_invitationRepo);
		_sut = new DeclineInvitationCommandHandler(_dbContext, _unitOfWork);
	}

	private static OrganizationInvitation CreatePendingInvitation() =>
		OrganizationInvitation.Create(OrgId, "Test Org", InviteeId, "Invitee Name", InviterId);

	[Test]
	public async Task Handle_ShouldDeclineInvitationAndSaveChanges_WhenPending(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreatePendingInvitation();
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var command = new DeclineInvitationCommand(invitation.Id, InviteeId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		invitation.Status.Should().Be(InvitationStatus.Declined);
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrowNotFound_WhenInvitationDoesNotExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitationId = OrganizationInvitationId.New();
		_invitationRepo.FindAsync(invitationId, cancellationToken).Returns((OrganizationInvitation?)null);
		var command = new DeclineInvitationCommand(invitationId, InviteeId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
		await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrowForbidden_WhenRequestingUserIsNotTheInvitee(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreatePendingInvitation();
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var someoneElse = UserId.New();
		var command = new DeclineInvitationCommand(invitation.Id, someoneElse);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		invitation.Status.Should().Be(InvitationStatus.Pending);
		await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrowConflict_WhenInvitationIsAlreadyDeclined(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreatePendingInvitation();
		invitation.Decline().ThrowIfFailure();
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var command = new DeclineInvitationCommand(invitation.Id, InviteeId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
		await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrowConflict_WhenInvitationIsAlreadyAccepted(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreatePendingInvitation();
		invitation.Accept().ThrowIfFailure();
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var command = new DeclineInvitationCommand(invitation.Id, InviteeId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
		await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}
}
