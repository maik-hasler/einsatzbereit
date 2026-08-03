using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Users.SetUserAdminStatus.v1;
using AwesomeAssertions;
using Domain.AuditLogs;
using NSubstitute;

namespace Application.UnitTests.Users.SetUserAdminStatus;

public class SetUserAdminStatusCommandHandlerTests
{
	private readonly IKeycloakUserService _keycloakService = Substitute.For<IKeycloakUserService>();
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<AuditLog, AuditLogId> _auditLogRepo =
		Substitute.For<IAggregateRepository<AuditLog, AuditLogId>>();
	private readonly SetUserAdminStatusCommandHandler _sut;

	public SetUserAdminStatusCommandHandlerTests()
	{
		_dbContext.AuditLogs.Returns(_auditLogRepo);
		_sut = new SetUserAdminStatusCommandHandler(_keycloakService, _dbContext);
	}

	[Test]
	public async Task Handle_ShouldAssignAdminRole_WhenPromotingAnotherUser(
		CancellationToken cancellationToken)
	{
		// Arrange
		var targetUserId = Guid.NewGuid();
		var actingUserId = Guid.NewGuid();
		_keycloakService.IsServiceAccountAsync(targetUserId, cancellationToken).Returns(false);
		var command = new SetUserAdminStatusCommand(targetUserId, actingUserId, IsAdmin: true);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _keycloakService.Received(1).AssignAdminRoleAsync(targetUserId, cancellationToken);
		await _keycloakService.DidNotReceive().RemoveAdminRoleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
		await _auditLogRepo.Received(1).AddAsync(
			Arg.Is<AuditLog>(a => a!.ActorUserId.Value == actingUserId
				&& a.ActionType == AuditActionType.UserPromotedToAdmin
				&& a.SubjectType == AuditSubjectType.User
				&& a.SubjectId == targetUserId),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldRemoveAdminRole_WhenDemotingAnotherUser(
		CancellationToken cancellationToken)
	{
		// Arrange
		var targetUserId = Guid.NewGuid();
		var actingUserId = Guid.NewGuid();
		_keycloakService.IsServiceAccountAsync(targetUserId, cancellationToken).Returns(false);
		var command = new SetUserAdminStatusCommand(targetUserId, actingUserId, IsAdmin: false);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _keycloakService.Received(1).RemoveAdminRoleAsync(targetUserId, cancellationToken);
		await _keycloakService.DidNotReceive().AssignAdminRoleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
		await _auditLogRepo.Received(1).AddAsync(
			Arg.Is<AuditLog>(a => a!.ActionType == AuditActionType.UserDemotedFromAdmin),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrowConflict_WhenActorDemotesThemselves(
		CancellationToken cancellationToken)
	{
		// Arrange
		var actingUserId = Guid.NewGuid();
		_keycloakService.IsServiceAccountAsync(actingUserId, cancellationToken).Returns(false);
		var command = new SetUserAdminStatusCommand(actingUserId, actingUserId, IsAdmin: false);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*own admin access*");
		await _keycloakService.DidNotReceive().RemoveAdminRoleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldAllowSelfPromotion_BecauseGuardOnlyBlocksDemoting(
		CancellationToken cancellationToken)
	{
		// Arrange - already-admin promoting "self" is a no-op-ish reassert, not the lockout footgun.
		var actingUserId = Guid.NewGuid();
		_keycloakService.IsServiceAccountAsync(actingUserId, cancellationToken).Returns(false);
		var command = new SetUserAdminStatusCommand(actingUserId, actingUserId, IsAdmin: true);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _keycloakService.Received(1).AssignAdminRoleAsync(actingUserId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrowForbidden_WhenTargetIsAServiceAccount(
		CancellationToken cancellationToken)
	{
		// Arrange
		var targetUserId = Guid.NewGuid();
		var actingUserId = Guid.NewGuid();
		_keycloakService.IsServiceAccountAsync(targetUserId, cancellationToken).Returns(true);
		var command = new SetUserAdminStatusCommand(targetUserId, actingUserId, IsAdmin: true);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*service account*");
		await _keycloakService.DidNotReceive().AssignAdminRoleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}
}
