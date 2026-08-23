using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Organizations.ChangeMemberRole.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Organizations.ChangeMemberRole;

public class ChangeMemberRoleCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly ChangeMemberRoleCommandHandler _sut;

	private static readonly OrganizationId OrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();
	private static readonly UserId TargetUserId = UserId.New();

	public ChangeMemberRoleCommandHandlerTests()
	{
		_dbContext
			.IsOrganizerAsync(OrgId, DefaultRequestingUserId, Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new ChangeMemberRoleCommandHandler(_dbContext, _keycloakService);
	}

	private void SetMembership(OrganizationMemberRole role)
	{
		var membership = OrganizationMembership.Create(OrgId, TargetUserId, role);
		_dbContext.GetMembershipAsync(OrgId, TargetUserId, Arg.Any<CancellationToken>()).Returns(membership);
	}

	[Test]
	public async Task Handle_ShouldPromoteAndAssignKeycloakRole_WhenChangingMemberToOrganizer(
		CancellationToken cancellationToken)
	{
		SetMembership(OrganizationMemberRole.Member);
		var command = new ChangeMemberRoleCommand(OrgId, TargetUserId, OrganizationMemberRole.Organizer, DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		await _keycloakService.Received(1).AssignOrganizerRoleAsync(TargetUserId.Value, cancellationToken);
		await _keycloakService.DidNotReceive().RevokeOrganizerRoleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldDemoteAndRevokeKeycloakRole_WhenTargetOrganizesNoOtherOrganization(
		CancellationToken cancellationToken)
	{
		SetMembership(OrganizationMemberRole.Organizer);
		_dbContext.CountOrganizersAsync(OrgId, Arg.Any<CancellationToken>()).Returns(2);
		_dbContext.GetOrganizerOrganizationsAsync(TargetUserId, Arg.Any<CancellationToken>()).Returns([]);
		var command = new ChangeMemberRoleCommand(OrgId, TargetUserId, OrganizationMemberRole.Member, DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		await _keycloakService.Received(1).RevokeOrganizerRoleAsync(TargetUserId.Value, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldDemoteButNotRevokeKeycloakRole_WhenTargetStillOrganizesAnotherOrganization(
		CancellationToken cancellationToken)
	{
		// Arrange - the realm role is shared across every org the user organizes (#1386),
		// so it must stay assigned while they still organize a different one.
		SetMembership(OrganizationMemberRole.Organizer);
		_dbContext.CountOrganizersAsync(OrgId, Arg.Any<CancellationToken>()).Returns(2);
		var otherOrg = Organization.Create(OrganizationId.New(), "Other Org").GetValueOrThrow();
		_dbContext.GetOrganizerOrganizationsAsync(TargetUserId, Arg.Any<CancellationToken>()).Returns([otherOrg]);
		var command = new ChangeMemberRoleCommand(OrgId, TargetUserId, OrganizationMemberRole.Member, DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		await _keycloakService.DidNotReceive().RevokeOrganizerRoleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrowConflict_WhenDemotingTheOnlyOrganizer(
		CancellationToken cancellationToken)
	{
		SetMembership(OrganizationMemberRole.Organizer);
		_dbContext.CountOrganizersAsync(OrgId, Arg.Any<CancellationToken>()).Returns(1);
		var command = new ChangeMemberRoleCommand(OrgId, TargetUserId, OrganizationMemberRole.Member, DefaultRequestingUserId);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
		await _keycloakService.DidNotReceive().RevokeOrganizerRoleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrowConflict_WhenRoleIsUnchanged(
		CancellationToken cancellationToken)
	{
		SetMembership(OrganizationMemberRole.Organizer);
		var command = new ChangeMemberRoleCommand(OrgId, TargetUserId, OrganizationMemberRole.Organizer, DefaultRequestingUserId);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
	}

	[Test]
	public async Task Handle_ShouldThrowNotFound_WhenMembershipDoesNotExist(
		CancellationToken cancellationToken)
	{
		_dbContext.GetMembershipAsync(OrgId, TargetUserId, Arg.Any<CancellationToken>()).Returns((OrganizationMembership?)null);
		var command = new ChangeMemberRoleCommand(OrgId, TargetUserId, OrganizationMemberRole.Organizer, DefaultRequestingUserId);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
	}

	[Test]
	public async Task Handle_ShouldThrowForbidden_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		_dbContext
			.IsOrganizerAsync(OrgId, DefaultRequestingUserId, Arg.Any<CancellationToken>())
			.Returns(false);
		var command = new ChangeMemberRoleCommand(OrgId, TargetUserId, OrganizationMemberRole.Organizer, DefaultRequestingUserId);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		await _dbContext.DidNotReceive().GetMembershipAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>());
	}
}
