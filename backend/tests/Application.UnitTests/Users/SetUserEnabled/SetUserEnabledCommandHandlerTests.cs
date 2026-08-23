using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Users.SetUserEnabled.v1;
using AwesomeAssertions;
using Domain.AuditLogs;
using NSubstitute;

namespace Application.UnitTests.Users.SetUserEnabled;

public class SetUserEnabledCommandHandlerTests
{
	private readonly IKeycloakUserService _keycloakService = Substitute.For<IKeycloakUserService>();
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<AuditLog, AuditLogId> _auditLogRepo =
		Substitute.For<IAggregateRepository<AuditLog, AuditLogId>>();
	private readonly SetUserEnabledCommandHandler _sut;

	public SetUserEnabledCommandHandlerTests()
	{
		_dbContext.AuditLogs.Returns(_auditLogRepo);
		_sut = new SetUserEnabledCommandHandler(_keycloakService, _dbContext);
	}

	[Test]
	public async Task Handle_ShouldDisableTargetUser_WhenNotSelf(
		CancellationToken cancellationToken)
	{
		var targetUserId = Guid.NewGuid();
		var actingUserId = Guid.NewGuid();
		_keycloakService.IsServiceAccountAsync(targetUserId, cancellationToken).Returns(false);
		var command = new SetUserEnabledCommand(targetUserId, actingUserId, Enabled: false);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		await _keycloakService.Received(1).SetUserEnabledAsync(targetUserId, false, cancellationToken);
		await _auditLogRepo.Received(1).AddAsync(
			Arg.Is<AuditLog>(a => a!.ActorUserId.Value == actingUserId
				&& a.ActionType == AuditActionType.UserDisabled
				&& a.SubjectType == AuditSubjectType.User
				&& a.SubjectId == targetUserId),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrowConflict_WhenActorDisablesTheirOwnAccount(
		CancellationToken cancellationToken)
	{
		var actingUserId = Guid.NewGuid();
		_keycloakService.IsServiceAccountAsync(actingUserId, cancellationToken).Returns(false);
		var command = new SetUserEnabledCommand(actingUserId, actingUserId, Enabled: false);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*own account*");
		await _keycloakService.DidNotReceive().SetUserEnabledAsync(
			Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldAllowEnablingSelf_BecauseGuardOnlyBlocksDisabling(
		CancellationToken cancellationToken)
	{
		// Arrange - re-enabling your own (already-active) account isn't the footgun the guard exists for.
		var actingUserId = Guid.NewGuid();
		_keycloakService.IsServiceAccountAsync(actingUserId, cancellationToken).Returns(false);
		var command = new SetUserEnabledCommand(actingUserId, actingUserId, Enabled: true);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		await _keycloakService.Received(1).SetUserEnabledAsync(actingUserId, true, cancellationToken);
		await _auditLogRepo.Received(1).AddAsync(
			Arg.Is<AuditLog>(a => a!.ActionType == AuditActionType.UserEnabled),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrowForbidden_WhenTargetIsAServiceAccount(
		CancellationToken cancellationToken)
	{
		var targetUserId = Guid.NewGuid();
		var actingUserId = Guid.NewGuid();
		_keycloakService.IsServiceAccountAsync(targetUserId, cancellationToken).Returns(true);
		var command = new SetUserEnabledCommand(targetUserId, actingUserId, Enabled: false);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*service account*");
		await _keycloakService.DidNotReceive().SetUserEnabledAsync(
			Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
	}
}
