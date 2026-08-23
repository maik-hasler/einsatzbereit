using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Organizations.GetOrgInvitations.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Organizations.GetOrgInvitations;

public class GetOrgInvitationsQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly GetOrgInvitationsQueryHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public GetOrgInvitationsQueryHandlerTests()
	{
		_dbContext
			.IsOrganizerAsync(DefaultOrgId, DefaultRequestingUserId, Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new GetOrgInvitationsQueryHandler(_dbContext, _keycloakUserService);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		_dbContext
			.IsOrganizerAsync(DefaultOrgId, DefaultRequestingUserId, cancellationToken)
			.Returns(false);
		var query = new GetOrgInvitationsQuery(DefaultOrgId, DefaultRequestingUserId);

		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*permission*");
		await _dbContext.DidNotReceive().GetInvitationsForOrganizationAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldReturnInvitations_WhenRequestingUserIsOrgMember(
		CancellationToken cancellationToken)
	{
		var inviteeId = UserId.New();
		var invitation = OrganizationInvitation.Create(
			DefaultOrgId, inviteeId, UserId.New(), OrganizationMemberRole.Organizer, DateTimeOffset.UtcNow);
		_dbContext.GetInvitationsForOrganizationAsync(DefaultOrgId, cancellationToken)
			.Returns([invitation]);
		_keycloakUserService
			.GetDisplayNamesAsync(Arg.Is<IReadOnlyList<Guid>>(ids => ids != null && ids.Contains(inviteeId.Value)), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, string> { [inviteeId.Value] = "Vera" });
		var query = new GetOrgInvitationsQuery(DefaultOrgId, DefaultRequestingUserId);

		var result = await _sut.Handle(query, cancellationToken);

		result.Should().HaveCount(1);
		result[0].Id.Should().Be(invitation.Id.Value);
		result[0].InviteeName.Should().Be("Vera");
		result[0].IntendedRole.Should().Be("Organizer");
		result[0].Status.Should().Be("Pending");
	}
}
