using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Organizations.DismissInvitation.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
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
			orgId, "Test Org", UserId.New(), "Vera", UserId.New());
		invitation.Decline();
		return invitation;
	}

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
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}
}
