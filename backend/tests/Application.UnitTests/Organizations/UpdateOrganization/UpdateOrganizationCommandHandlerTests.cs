using Application.Common.Persistence;
using Application.Organizations.UpdateOrganization.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using NSubstitute;
using NSubstitute.ExceptionExtensions;


namespace Application.UnitTests.Organizations.UpdateOrganization;

public class UpdateOrganizationCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Organization, OrganizationId> _orgRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly UpdateOrganizationCommandHandler _sut;

	public UpdateOrganizationCommandHandlerTests()
	{
		_dbContext.Organizations.Returns(_orgRepo);
		_sut = new UpdateOrganizationCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldUpdateOrganization_WithAllFields(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var org = Organization.Create(new OrganizationId(orgId), "Old Name");

		_orgRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns(org);

		var command = new UpdateOrganizationCommand(
			orgId,
			"New Name",
			"A Description",
			"contact@test.com",
			"+49 123 456",
			"https://example.org",
			new UpdateAddressCommand("Main Street", "1", "12345", "Berlin"));

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
		var orgId = Guid.NewGuid();
		var org = Organization.Create(new OrganizationId(orgId), "Org");

		_orgRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns(org);

		var command = new UpdateOrganizationCommand(
			orgId, "Org", null, null, null, null, null);

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
		var orgId = Guid.NewGuid();

		_orgRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns((Organization?)null);

		var command = new UpdateOrganizationCommand(
			orgId, "Name", null, null, null, null, null);

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
		var orgId = Guid.NewGuid();
		var org = Organization.Create(new OrganizationId(orgId), "Org");

		_orgRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns(org);

		var command = new UpdateOrganizationCommand(
			orgId, "   ", null, null, null, null, null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage("*Name must not be empty*");
	}
}
