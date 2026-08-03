using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Users.AdminRestoreUser.v1;
using AwesomeAssertions;
using Domain.AuditLogs;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.AdminRestoreUser;

public class AdminRestoreUserCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<AuditLog, AuditLogId> _auditLogRepo =
		Substitute.For<IAggregateRepository<AuditLog, AuditLogId>>();
	private readonly AdminRestoreUserCommandHandler _sut;

	private static readonly UserId DefaultAdminUserId = UserId.New();

	public AdminRestoreUserCommandHandlerTests()
	{
		_dbContext.AuditLogs.Returns(_auditLogRepo);
		_sut = new AdminRestoreUserCommandHandler(_dbContext);
	}

	private static User CreateShadowDeletedUser(UserId userId)
	{
		var user = User.Create(userId);
		user.MarkDeleted(DateTimeOffset.UtcNow);
		return user;
	}

	[Test]
	public async Task Handle_ShouldRestoreUser_AndWriteAuditLog(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = Guid.NewGuid();
		var user = CreateShadowDeletedUser(UserId.Create(userId).GetValueOrThrow());
		_dbContext.FindUserIncludingDeletedAsync(UserId.Create(userId).GetValueOrThrow(), cancellationToken).Returns(user);

		// Act
		var result = await _sut.Handle(new AdminRestoreUserCommand(userId, DefaultAdminUserId), cancellationToken);

		// Assert
		result.Should().BeTrue();
		user.IsDeleted.Should().BeFalse();
		await _auditLogRepo.Received(1).AddAsync(
			Arg.Is<AuditLog>(a => a!.ActorUserId == DefaultAdminUserId
				&& a.ActionType == AuditActionType.UserRestored
				&& a.SubjectType == AuditSubjectType.User
				&& a.SubjectId == userId),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenUserNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = Guid.NewGuid();
		_dbContext.FindUserIncludingDeletedAsync(UserId.Create(userId).GetValueOrThrow(), cancellationToken).Returns((User?)null);

		// Act
		Func<Task> act = async () => await _sut.Handle(new AdminRestoreUserCommand(userId, DefaultAdminUserId), cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenUserNotShadowDeleted(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = Guid.NewGuid();
		var user = User.Create(UserId.Create(userId).GetValueOrThrow());
		_dbContext.FindUserIncludingDeletedAsync(UserId.Create(userId).GetValueOrThrow(), cancellationToken).Returns(user);

		// Act
		Func<Task> act = async () => await _sut.Handle(new AdminRestoreUserCommand(userId, DefaultAdminUserId), cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*not shadow-deleted*");
		await _auditLogRepo.DidNotReceive().AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
	}
}
