using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Users.Unsubscribe.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.Unsubscribe;

public class UnsubscribeCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IAggregateRepository<User, UserId> _userRepo =
		Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly UnsubscribeCommandHandler _sut;

	public UnsubscribeCommandHandlerTests()
	{
		_dbContext.Users.Returns(_userRepo);
		_sut = new UnsubscribeCommandHandler(_dbContext, _unitOfWork);
	}

	[Test]
	public async Task Handle_ShouldDisableTheRequestedType_WhenTokenMatches(
		CancellationToken cancellationToken)
	{
		var user = User.Create(UserId.New());
		_userRepo.FindAsync(user.Id, cancellationToken).Returns(user);
		var command = new UnsubscribeCommand(user.Id, user.UnsubscribeToken, EmailNotificationType.EngagementConfirmed);

		await _sut.Handle(command, cancellationToken);

		user.NotifyOnEngagementConfirmed.Should().BeFalse();
		user.NotifyOnNewSignUp.Should().BeTrue();
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenTokenDoesNotMatch(
		CancellationToken cancellationToken)
	{
		var user = User.Create(UserId.New());
		_userRepo.FindAsync(user.Id, cancellationToken).Returns(user);
		var command = new UnsubscribeCommand(user.Id, Guid.NewGuid(), EmailNotificationType.EngagementConfirmed);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>();
		user.NotifyOnEngagementConfirmed.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenUserDoesNotExist(
		CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		_userRepo.FindAsync(userId, cancellationToken).Returns((User?)null);
		var command = new UnsubscribeCommand(userId, Guid.NewGuid(), EmailNotificationType.NewSignUp);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*not found*");
	}
}
