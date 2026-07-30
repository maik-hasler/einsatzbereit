using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Organizations.GetOrgInvitations.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;


namespace Application.UnitTests.Organizations.GetOrgInvitations;

public class GetOrgInvitationsQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly GetOrgInvitationsQueryHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public GetOrgInvitationsQueryHandlerTests()
	{
		_dbContext
			.IsOrganizerAsync(DefaultOrgId, DefaultRequestingUserId, Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new GetOrgInvitationsQueryHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		// Arrange
		_dbContext
			.IsOrganizerAsync(DefaultOrgId, DefaultRequestingUserId, cancellationToken)
			.Returns(false);
		var query = new GetOrgInvitationsQuery(DefaultOrgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*permission*");
		await _dbContext.DidNotReceive().GetInvitationsForOrganizationAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldReturnInvitations_WhenRequestingUserIsOrgMember(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = OrganizationInvitation.Create(
			DefaultOrgId, "Test Org", UserId.New(), "Vera", UserId.New(), OrganizationMemberRole.Organizer, DateTimeOffset.UtcNow);
		_dbContext.GetInvitationsForOrganizationAsync(DefaultOrgId, cancellationToken)
			.Returns([invitation]);
		var query = new GetOrgInvitationsQuery(DefaultOrgId, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().HaveCount(1);
		result[0].Id.Should().Be(invitation.Id.Value);
		result[0].InviteeName.Should().Be("Vera");
		result[0].IntendedRole.Should().Be("Organizer");
		result[0].Status.Should().Be("Pending");
	}
}
