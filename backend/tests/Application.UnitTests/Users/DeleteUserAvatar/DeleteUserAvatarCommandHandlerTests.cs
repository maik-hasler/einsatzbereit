using Application.Common.Persistence;
using Application.Common.Storage;
using Application.Users.DeleteUserAvatar.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Application.UnitTests.Users.DeleteUserAvatar;

public class DeleteUserAvatarCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
	private readonly DeleteUserAvatarCommandHandler _sut;

	public DeleteUserAvatarCommandHandlerTests()
	{
		_sut = new DeleteUserAvatarCommandHandler(_dbContext, _fileStorage);
	}

	[Test]
	public async Task Handle_ShouldClearAvatarUrl_AndDeleteTheStorageObject_WhenUserHasAnAvatar(
		CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		var user = User.Create(userId);
		user.SetAvatarUrl("https://example.com/user-avatars/some-key/avatar.png");
		_dbContext.GetOrCreateUserAsync(userId, Arg.Any<string?>(), cancellationToken).Returns(user);
		_fileStorage
			.GetObjectKeyFromPublicUrl("https://example.com/user-avatars/some-key/avatar.png")
			.Returns("user-avatars/some-key/avatar.png");

		var command = new DeleteUserAvatarCommand(userId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		user.AvatarUrl.Should().BeNull();
		await _fileStorage.Received(1).DeleteAsync("user-avatars/some-key/avatar.png", cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldBeANoop_WhenUserHasNoAvatar(
		CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		var user = User.Create(userId);
		_dbContext.GetOrCreateUserAsync(userId, Arg.Any<string?>(), cancellationToken).Returns(user);

		var command = new DeleteUserAvatarCommand(userId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		user.AvatarUrl.Should().BeNull();
		await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotAttemptDeletion_WhenTheStoredUrlCannotBeMappedToAnObjectKey(
		CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		var user = User.Create(userId);
		user.SetAvatarUrl("https://example.com/user-avatars/some-key/avatar.png");
		_dbContext.GetOrCreateUserAsync(userId, Arg.Any<string?>(), cancellationToken).Returns(user);
		_fileStorage
			.GetObjectKeyFromPublicUrl("https://example.com/user-avatars/some-key/avatar.png")
			.Returns((string?)null);

		var command = new DeleteUserAvatarCommand(userId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		user.AvatarUrl.Should().BeNull();
		await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotThrow_AndStillClearAvatarUrl_WhenDeletingTheStorageObjectFails(
		CancellationToken cancellationToken)
	{
		// Arrange: the local field is what the user/UI actually observes - a
		// storage cleanup failure just leaves an orphaned object behind rather
		// than blocking the removal (mirrors UploadUserAvatarCommandHandler's
		// best-effort cleanup of the previous avatar).
		var userId = UserId.New();
		var user = User.Create(userId);
		user.SetAvatarUrl("https://example.com/user-avatars/some-key/avatar.png");
		_dbContext.GetOrCreateUserAsync(userId, Arg.Any<string?>(), cancellationToken).Returns(user);
		_fileStorage
			.GetObjectKeyFromPublicUrl("https://example.com/user-avatars/some-key/avatar.png")
			.Returns("user-avatars/some-key/avatar.png");
		_fileStorage
			.DeleteAsync("user-avatars/some-key/avatar.png", Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("MinIO unavailable"));

		var command = new DeleteUserAvatarCommand(userId);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().NotThrowAsync();
		user.AvatarUrl.Should().BeNull();
	}
}
