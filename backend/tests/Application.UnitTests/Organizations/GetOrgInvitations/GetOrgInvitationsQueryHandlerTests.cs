using Application.Common.Keycloak;
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
	private readonly IKeycloakOrganizationService _keycloakOrgService = Substitute.For<IKeycloakOrganizationService>();
	private readonly GetOrgInvitationsQueryHandler _sut;

	private static readonly OrganizationId DefaultOrgId = new(Guid.CreateVersion7());
	private static readonly UserId DefaultRequestingUserId = new(Guid.CreateVersion7());

	public GetOrgInvitationsQueryHandlerTests()
	{
		_keycloakOrgService
			.GetUserOrganizationsAsync(DefaultRequestingUserId.Value, Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganization(DefaultOrgId.Value, "Test Org")]);
		_sut = new GetOrgInvitationsQueryHandler(_dbContext, _keycloakOrgService);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		// Arrange
		_keycloakOrgService
			.GetUserOrganizationsAsync(DefaultRequestingUserId.Value, cancellationToken)
			.Returns([]);
		var query = new GetOrgInvitationsQuery(DefaultOrgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage("*permission*");
		await _dbContext.DidNotReceive().GetInvitationsForOrganizationAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldReturnInvitations_WhenRequestingUserIsOrgMember(
		CancellationToken cancellationToken)
	{
		// Arrange
		var invitation = OrganizationInvitation.Create(
			DefaultOrgId, "Test Org", new UserId(Guid.CreateVersion7()), "Vera", new UserId(Guid.CreateVersion7()));
		_dbContext.GetInvitationsForOrganizationAsync(DefaultOrgId, cancellationToken)
			.Returns([invitation]);
		var query = new GetOrgInvitationsQuery(DefaultOrgId, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().HaveCount(1);
		result[0].Id.Should().Be(invitation.Id.Value);
		result[0].InviteeName.Should().Be("Vera");
		result[0].Status.Should().Be("Pending");
	}
}
