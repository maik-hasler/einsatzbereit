using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Common.Storage;
using Application.Organizations.UploadOrganizationLogo.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Organizations.UploadOrganizationLogo;

public class UploadOrganizationLogoCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Organization, OrganizationId> _orgRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
	private readonly UploadOrganizationLogoCommandHandler _sut;

	private static readonly UserId DefaultRequestingUserId = UserId.New();
	private static byte[] PngBytes => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

	public UploadOrganizationLogoCommandHandlerTests()
	{
		_dbContext.Organizations.Returns(_orgRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_fileStorage
			.UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns("https://example.com/organization-logos/logo.png");
		_sut = new UploadOrganizationLogoCommandHandler(_dbContext, _fileStorage);
	}

	private static Organization CreateOrganization(Guid orgId) =>
		Organization.Create(OrganizationId.Create(orgId).GetValueOrThrow(), "Test Org").Value;

	[Test]
	public async Task Handle_ShouldSetLogoUrl_WhenOrganizationExists(
		CancellationToken cancellationToken)
	{
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_orgRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);

		var command = new UploadOrganizationLogoCommand(orgId, PngBytes, "image/png", DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		organization.LogoUrl.Should().Be("https://example.com/organization-logos/logo.png");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange: caller is not an organizer of this organization.
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_orgRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var command = new UploadOrganizationLogoCommand(orgId, PngBytes, "image/png", DefaultRequestingUserId);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		organization.LogoUrl.Should().BeNull();
		await _fileStorage.DidNotReceive().UploadAsync(
			Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}
}
