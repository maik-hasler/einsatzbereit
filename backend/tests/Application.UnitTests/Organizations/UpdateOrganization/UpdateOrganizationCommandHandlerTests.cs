using Application.Common.Persistence;
using Application.Organizations.UpdateOrganization.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;
using NSubstitute.ExceptionExtensions;


namespace Application.UnitTests.Organizations.UpdateOrganization;

public class UpdateOrganizationCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Organization, OrganizationId> _orgRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly UpdateOrganizationCommandHandler _sut;

	private static readonly Guid DefaultOrgId = Guid.NewGuid();
	private static readonly UserId DefaultRequestingUserId = new(Guid.CreateVersion7());

	public UpdateOrganizationCommandHandlerTests()
	{
		_dbContext.Organizations.Returns(_orgRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new UpdateOrganizationCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldUpdateOrganization_WithAllFields(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = DefaultOrgId;
		var org = Organization.Create(new OrganizationId(orgId), "Old Name");

		_orgRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns(org);

		var command = new UpdateOrganizationCommand(
			orgId,
			"New Name",
			"A Description",
			"contact@test.com",
			"+49 123 456",
			"https://example.org",
			new UpdateAddressCommand("Main Street", "1", "12345", "Berlin"),
			DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		org.Name.Should().Be("New Name");
		org.Description.Should().Be("A Description");
		org.ContactEmail.Should().Be("contact@test.com");
		org.ContactPhone.Should().Be("+49 123 456");
		org.Website.Should().Be("https://example.org");
		org.Address.Should().NotBeNull();
		org.Address!.Street.Should().Be("Main Street");
		org.Address.City.Should().Be("Berlin");
	}

	[Test]
	public async Task Handle_ShouldClearOptionalFields_WhenNullProvided(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = DefaultOrgId;
		var org = Organization.Create(new OrganizationId(orgId), "Org");

		_orgRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns(org);

		var command = new UpdateOrganizationCommand(
			orgId, "Org", null, null, null, null, null, DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		org.Description.Should().BeNull();
		org.ContactEmail.Should().BeNull();
		org.Address.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOrganizationNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = DefaultOrgId;

		_orgRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns((Organization?)null);

		var command = new UpdateOrganizationCommand(
			orgId, "Name", null, null, null, null, null, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>();
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenNameIsEmpty(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = DefaultOrgId;
		var org = Organization.Create(new OrganizationId(orgId), "Org");

		_orgRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns(org);

		var command = new UpdateOrganizationCommand(
			orgId, "   ", null, null, null, null, null, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage("*Name must not be empty*");
	}
}
