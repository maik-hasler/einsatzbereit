using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Common.Storage;
using Application.Organizations.DeleteOrganizationLogo.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Organizations.DeleteOrganizationLogo;

public class DeleteOrganizationLogoCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Organization, OrganizationId> _orgRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
	private readonly DeleteOrganizationLogoCommandHandler _sut;

	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public DeleteOrganizationLogoCommandHandlerTests()
	{
		_dbContext.Organizations.Returns(_orgRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new DeleteOrganizationLogoCommandHandler(_dbContext, _fileStorage);
	}

	private static Organization CreateOrganizationWithLogo(Guid orgId)
	{
		var org = Organization.Create(OrganizationId.Create(orgId).GetValueOrThrow(), "Test Org").Value;
		org.SetLogoUrl("https://example.com/logo.png");
		return org;
	}

	[Test]
	public async Task Handle_ShouldClearLogoUrl_WhenOrganizationExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganizationWithLogo(orgId);
		_orgRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);

		var command = new DeleteOrganizationLogoCommand(orgId, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		organization.LogoUrl.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganizationWithLogo(orgId);
		_orgRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var command = new DeleteOrganizationLogoCommand(orgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		organization.LogoUrl.Should().NotBeNull();
		await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}
}
