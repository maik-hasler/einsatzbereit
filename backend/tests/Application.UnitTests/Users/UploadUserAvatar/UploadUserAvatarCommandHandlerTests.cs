using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Common.Storage;
using Application.Users.UploadUserAvatar.v1;
using AwesomeAssertions;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.UploadUserAvatar;

public class UploadUserAvatarCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<User, UserId> _usersRepo = Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
	private readonly UploadUserAvatarCommandHandler _sut;

	private static byte[] PngBytes => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

	public UploadUserAvatarCommandHandlerTests()
	{
		_dbContext.Users.Returns(_usersRepo);
		_fileStorage
			.UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns("https://example.com/user-avatars/avatar.png");
		_sut = new UploadUserAvatarCommandHandler(_dbContext, _fileStorage);
	}

	[Test]
	public async Task Handle_ShouldSetAvatarUrl_WhenUserAlreadyExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = UserId.New();
		var user = User.Create(userId);
		_usersRepo.FindAsync(userId, cancellationToken).Returns(user);

		var command = new UploadUserAvatarCommand(userId, PngBytes, "image/png");

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		user.AvatarUrl.Should().Be("https://example.com/user-avatars/avatar.png");
		await _usersRepo.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldCreateUserAndSetAvatarUrl_WhenUserDoesNotExistYet(
		CancellationToken cancellationToken)
	{
		// Arrange: RecordActivity/UploadUserAvatar can be the very first write for a
		// user who has only ever authenticated via Keycloak - there is no
		// registration step that creates the local User row up front.
		var userId = UserId.New();
		_usersRepo.FindAsync(userId, cancellationToken).Returns((User?)null);

		var command = new UploadUserAvatarCommand(userId, PngBytes, "image/png");

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _usersRepo.Received(1).AddAsync(
			Arg.Is<User>(u => u!.Id == userId && u.AvatarUrl == "https://example.com/user-avatars/avatar.png"),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_AndNotUpload_WhenContentTypeIsInvalid(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = UserId.New();
		var command = new UploadUserAvatarCommand(userId, PngBytes, "application/pdf");

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Validation);
		await _fileStorage.DidNotReceive().UploadAsync(
			Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
		await _usersRepo.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_AndNotUpload_WhenContentBytesDoNotMatchDeclaredContentType(
		CancellationToken cancellationToken)
	{
		// Arrange: declared content-type is allowed, but the magic bytes don't match
		// any known image signature - ImageUploadValidator detects the mismatch from
		// the actual bytes rather than trusting the client-supplied header.
		var userId = UserId.New();
		var notActuallyAnImage = "not a real image"u8.ToArray();
		var command = new UploadUserAvatarCommand(userId, notActuallyAnImage, "image/png");

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Validation);
		await _fileStorage.DidNotReceive().UploadAsync(
			Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}
}
