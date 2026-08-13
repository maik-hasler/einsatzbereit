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
			.SearchUsersAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_keycloakService
			.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_dbContext
			.GetInvitationsForOrganizationAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_sut = new SearchMemberCandidatesQueryHandler(_dbContext, _keycloakService);
	}

	[Test]
	public async Task Handle_ShouldReturnCandidates_ExcludingExistingMembers(
		CancellationToken cancellationToken)
	{
		// Arrange
		var existingMember = Guid.NewGuid();
		var candidate = Guid.NewGuid();
		_keycloakService
			.SearchUsersAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns((IReadOnlyList<KeycloakOrganizationMember>)
			[
				new KeycloakOrganizationMember(existingMember, "olaf", "Olaf", "Miller", "olaf@test.de", true),
				new KeycloakOrganizationMember(candidate, "vera", "Vera", "Smith", "vera@test.de", false),
			]);
		_keycloakService
			.GetMembersAsync(DefaultOrgId, cancellationToken)
			.Returns((IReadOnlyList<KeycloakOrganizationMember>)
			[
				new KeycloakOrganizationMember(existingMember, "olaf", "Olaf", "Miller", "olaf@test.de", true),
			]);

		var query = new SearchMemberCandidatesQuery(DefaultOrgId, "v", DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().ContainSingle(c => c.UserId == candidate);
	}

	[Test]
	public async Task Handle_ShouldReturnCandidates_ExcludingUsersWithPendingInvitation(
		CancellationToken cancellationToken)
	{
		// Arrange
		var pendingInvitee = Guid.NewGuid();
		var candidate = Guid.NewGuid();
		_keycloakService
			.SearchUsersAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns((IReadOnlyList<KeycloakOrganizationMember>)
			[
				new KeycloakOrganizationMember(pendingInvitee, "olaf", "Olaf", "Miller", "olaf@test.de", false),
				new KeycloakOrganizationMember(candidate, "vera", "Vera", "Smith", "vera@test.de", false),
			]);

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

		var query = new SearchMemberCandidatesQuery(DefaultOrgId, "v", DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().ContainSingle(c => c.UserId == candidate);
	}

	[Test]
	public async Task Handle_ShouldReturnCandidate_WhenInvitationIsNotPending(
		CancellationToken cancellationToken)
	{
		// Arrange: a Declined invitation shouldn't block the invitee from
		// being re-invited via search.
		var declinedInvitee = Guid.NewGuid();
		_keycloakService
			.SearchUsersAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns((IReadOnlyList<KeycloakOrganizationMember>)
			[
				new KeycloakOrganizationMember(declinedInvitee, "olaf", "Olaf", "Miller", "olaf@test.de", false),
			]);

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

		var query = new SearchMemberCandidatesQuery(DefaultOrgId, "v", DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().ContainSingle(c => c.UserId == declinedInvitee);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var query = new SearchMemberCandidatesQuery(DefaultOrgId, "v", DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		await _keycloakService.DidNotReceive().SearchUsersAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
	}
}
