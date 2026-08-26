using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Users.ReportUser.v1;
using AwesomeAssertions;
using Domain.Primitives;
using Domain.Reports;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.ReportUser;

public class ReportUserCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<User, UserId> _userRepo =
		Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly IAggregateRepository<Report, ReportId> _reportRepo =
		Substitute.For<IAggregateRepository<Report, ReportId>>();
	private readonly ReportUserCommandHandler _sut;

	private static readonly UserId DefaultReporterId = UserId.New();

	public ReportUserCommandHandlerTests()
	{
		_dbContext.Users.Returns(_userRepo);
		_dbContext.Reports.Returns(_reportRepo);
		_dbContext
			.HasDuplicateReportAsync(Arg.Any<ReportTargetType>(), Arg.Any<Guid>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);
		_sut = new ReportUserCommandHandler(_dbContext);
	}

	private static User CreateUser(Guid id) => User.Create(UserId.Create(id).GetValueOrThrow());

	[Test]
	public async Task Handle_ShouldAddReport_WhenUserExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var targetUserGuid = Guid.CreateVersion7();
		var targetUser = CreateUser(targetUserGuid);
		_userRepo
			.FindAsync(UserId.Create(targetUserGuid).GetValueOrThrow(), cancellationToken)
			.Returns(targetUser);

		var command = new ReportUserCommand(targetUserGuid, DefaultReporterId, ReportReason.Harassment, "rude messages");

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _reportRepo.Received(1).AddAsync(
			Arg.Is<Report>(r => r!.TargetType == ReportTargetType.User
				&& r.TargetId == targetUserGuid
				&& r.ReporterId == DefaultReporterId
				&& r.Reason == ReportReason.Harassment
				&& r.Details == "rude messages"),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenReportingSelf(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new ReportUserCommand(DefaultReporterId.Value, DefaultReporterId, ReportReason.Spam, null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Validation);
		await _userRepo.DidNotReceive().FindAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
		await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenUserNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var targetUserGuid = Guid.CreateVersion7();
		_userRepo
			.FindAsync(UserId.Create(targetUserGuid).GetValueOrThrow(), cancellationToken)
			.Returns((User?)null);

		var command = new ReportUserCommand(targetUserGuid, DefaultReporterId, ReportReason.Spam, null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
		await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenReporterAlreadyHasOpenReport(
		CancellationToken cancellationToken)
	{
		// Arrange
		var targetUserGuid = Guid.CreateVersion7();
		var targetUser = CreateUser(targetUserGuid);
		_userRepo
			.FindAsync(UserId.Create(targetUserGuid).GetValueOrThrow(), cancellationToken)
			.Returns(targetUser);
		_dbContext
			.HasDuplicateReportAsync(ReportTargetType.User, targetUserGuid, DefaultReporterId, cancellationToken)
			.Returns(true);

		var command = new ReportUserCommand(targetUserGuid, DefaultReporterId, ReportReason.Spam, null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
		await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenDetailsTooLong(
		CancellationToken cancellationToken)
	{
		// Arrange
		var targetUserGuid = Guid.CreateVersion7();
		var targetUser = CreateUser(targetUserGuid);
		_userRepo
			.FindAsync(UserId.Create(targetUserGuid).GetValueOrThrow(), cancellationToken)
			.Returns(targetUser);

		var tooLong = new string('a', Report.MaxDetailsLength + 1);
		var command = new ReportUserCommand(targetUserGuid, DefaultReporterId, ReportReason.Other, tooLong);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Validation);
		await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
	}
}
