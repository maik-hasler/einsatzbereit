using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Organizations.SearchMemberCandidates.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Organizations.SearchMemberCandidates;

public class SearchMemberCandidatesQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly SearchMemberCandidatesQueryHandler _sut;

	private static readonly Guid DefaultOrgId = Guid.NewGuid();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public SearchMemberCandidatesQueryHandlerTests()
	{
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_keycloakService
			.FindUserByExactMatchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns((KeycloakOrganizationMember?)null);
		_keycloakService
			.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_dbContext
			.GetInvitationsForOrganizationAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_sut = new SearchMemberCandidatesQueryHandler(_dbContext, _keycloakService);
	}

	[Test]
	public async Task Handle_ShouldReturnEmpty_WhenNoExactMatchExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var query = new SearchMemberCandidatesQuery(DefaultOrgId, "nosuchuser", DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().BeEmpty();
		await _keycloakService.DidNotReceive().GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldReturnAvailable_WhenMatchIsNotAMemberOrInvitee(
		CancellationToken cancellationToken)
	{
		// Arrange
		var candidate = Guid.NewGuid();
		_keycloakService
			.FindUserByExactMatchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakOrganizationMember(candidate, "vera", "Vera", "Smith", "vera@test.de", false));

		var query = new SearchMemberCandidatesQuery(DefaultOrgId, "vera", DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		var single = result.Should().ContainSingle().Which;
		single.UserId.Should().Be(candidate);
		single.Status.Should().Be(MemberCandidateStatus.Available.ToString());
	}

	[Test]
	public async Task Handle_ShouldReturnAlreadyMember_WhenMatchIsAnExistingMember(
		CancellationToken cancellationToken)
	{
		// Arrange
		var existingMember = Guid.NewGuid();
		_keycloakService
			.FindUserByExactMatchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakOrganizationMember(existingMember, "olaf", "Olaf", "Miller", "olaf@test.de", true));
		_keycloakService
			.GetMembersAsync(DefaultOrgId, cancellationToken)
			.Returns((IReadOnlyList<KeycloakOrganizationMember>)
			[
				new KeycloakOrganizationMember(existingMember, "olaf", "Olaf", "Miller", "olaf@test.de", true),
			]);

		var query = new SearchMemberCandidatesQuery(DefaultOrgId, "olaf", DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().ContainSingle().Which.Status.Should().Be(MemberCandidateStatus.AlreadyMember.ToString());
	}

	[Test]
	public async Task Handle_ShouldReturnAlreadyInvited_WhenMatchHasAPendingInvitation(
		CancellationToken cancellationToken)
	{
		// Arrange
		var pendingInvitee = Guid.NewGuid();
		_keycloakService
			.FindUserByExactMatchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakOrganizationMember(pendingInvitee, "vera", "Vera", "Smith", "vera@test.de", false));

		var organizationId = OrganizationId.Create(DefaultOrgId).GetValueOrThrow();
		var pendingInvitation = OrganizationInvitation.Create(
			organizationId,
			UserId.Create(pendingInvitee).GetValueOrThrow(),
			DefaultRequestingUserId,
			OrganizationMemberRole.Member,
			DateTimeOffset.UtcNow);
		_dbContext
			.GetInvitationsForOrganizationAsync(organizationId, cancellationToken)
			.Returns([pendingInvitation]);

		var query = new SearchMemberCandidatesQuery(DefaultOrgId, "vera", DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().ContainSingle().Which.Status.Should().Be(MemberCandidateStatus.AlreadyInvited.ToString());
	}

	[Test]
	public async Task Handle_ShouldReturnAvailable_WhenInvitationIsNotPending(
		CancellationToken cancellationToken)
	{
		// Arrange
		var declinedInvitee = Guid.NewGuid();
		_keycloakService
			.FindUserByExactMatchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakOrganizationMember(declinedInvitee, "olaf", "Olaf", "Miller", "olaf@test.de", false));

		var organizationId = OrganizationId.Create(DefaultOrgId).GetValueOrThrow();
		var declinedInvitation = OrganizationInvitation.Create(
			organizationId,
			UserId.Create(declinedInvitee).GetValueOrThrow(),
			DefaultRequestingUserId,
			OrganizationMemberRole.Member,
			DateTimeOffset.UtcNow);
		declinedInvitation.Decline();
		_dbContext
			.GetInvitationsForOrganizationAsync(organizationId, cancellationToken)
			.Returns([declinedInvitation]);

		var query = new SearchMemberCandidatesQuery(DefaultOrgId, "olaf", DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().ContainSingle().Which.Status.Should().Be(MemberCandidateStatus.Available.ToString());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var query = new SearchMemberCandidatesQuery(DefaultOrgId, "vera", DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		await _keycloakService.DidNotReceive().FindUserByExactMatchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}
}
