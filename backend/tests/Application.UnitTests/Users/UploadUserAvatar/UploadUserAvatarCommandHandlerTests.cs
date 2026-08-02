using Application.Common.Persistence;
using Application.Common.Storage;
using Application.Users.UploadUserAvatar.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.UploadUserAvatar;

public class UploadUserAvatarCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
	private readonly UploadUserAvatarCommandHandler _sut;

	private static readonly UserId DefaultUserId = UserId.New();

	// Minimal valid PNG signature - ImageUploadValidator detects content type from
	// the actual magic bytes, not the client-declared header.
	private static readonly byte[] PngContent =
		[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];

	public UploadUserAvatarCommandHandlerTests()
	{
		_sut = new UploadUserAvatarCommandHandler(_dbContext, _fileStorage);
	}

	[Test]
	public async Task Handle_ShouldSetAvatarUrl_OnALazilyCreatedUserRow(
		CancellationToken cancellationToken)
	{
		// #1148: the row is fetched-or-created via the idempotent GetOrCreateUserAsync
		// rather than a check-then-Add the handler used to do itself.
		var user = User.Create(DefaultUserId);
		_dbContext.GetOrCreateUserAsync(DefaultUserId, Arg.Any<string?>(), cancellationToken).Returns(user);
		_fileStorage
			.UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns("https://example.com/user-avatars/avatar.png");
		var command = new UploadUserAvatarCommand(DefaultUserId, PngContent, "image/png");

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		user.AvatarUrl.Should().Be("https://example.com/user-avatars/avatar.png");
	}

	[Test]
	public async Task Handle_ShouldSetAvatarUrl_OnExistingUserRow(
		CancellationToken cancellationToken)
	{
		var user = User.Create(DefaultUserId);
		_dbContext.GetOrCreateUserAsync(DefaultUserId, Arg.Any<string?>(), cancellationToken).Returns(user);
		_fileStorage
			.UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns("https://example.com/user-avatars/avatar.png");
		var command = new UploadUserAvatarCommand(DefaultUserId, PngContent, "image/png");

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _fileStorage.Received(1).UploadAsync(
			$"user-avatars/{DefaultUserId.Value}.png", Arg.Any<Stream>(), PngContent.Length, "image/png", cancellationToken);
	}
}
