using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Invitations.AcceptInvitation.v1;
using AwesomeAssertions;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.UnitTests.Invitations.AcceptInvitation;

public class OrganizationInvitationAcceptedDomainEventHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IAggregateRepository<OrganizationInvitation, OrganizationInvitationId> _invitationRepo =
		Substitute.For<IAggregateRepository<OrganizationInvitation, OrganizationInvitationId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly OrganizationInvitationAcceptedDomainEventHandler _sut;

	private static readonly OrganizationId OrgId = OrganizationId.New();
	private static readonly UserId InviteeId = UserId.New();
	private static readonly UserId InviterId = UserId.New();

	public OrganizationInvitationAcceptedDomainEventHandlerTests()
	{
		_dbContext.OrganizationInvitations.Returns(_invitationRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_sut = new OrganizationInvitationAcceptedDomainEventHandler(
			_dbContext, _unitOfWork, NullLogger<OrganizationInvitationAcceptedDomainEventHandler>.Instance);
	}

	private static OrganizationInvitation CreateAcceptedInvitation()
	{
		var invitation = OrganizationInvitation.Create(OrgId, InviteeId, InviterId, OrganizationMemberRole.Member, DateTimeOffset.UtcNow);
		invitation.Accept().ThrowIfFailure();
		return invitation;
	}

	[Test]
	public async Task Handle_ShouldCreateInAppNotification_ForTheInvitingOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreateAcceptedInvitation();
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var domainEvent = new OrganizationInvitationAcceptedDomainEvent(invitation.Id, OrgId, InviteeId);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.RecipientId == InviterId
				&& n.Kind == NotificationKind.InvitationAccepted
				&& n.RelatedEntityId == invitation.Id.Value),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSaveChanges_AfterNotifying(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = CreateAcceptedInvitation();
		_invitationRepo.FindAsync(invitation.Id, cancellationToken).Returns(invitation);
		var domainEvent = new OrganizationInvitationAcceptedDomainEvent(invitation.Id, OrgId, InviteeId);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotThrow_WhenInvitationNoLongerExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitationId = OrganizationInvitationId.New();
		_invitationRepo.FindAsync(invitationId, cancellationToken).Returns((OrganizationInvitation?)null);
		var domainEvent = new OrganizationInvitationAcceptedDomainEvent(invitationId, OrgId, InviteeId);

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		await _notifRepo.DidNotReceive().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
	}
}
