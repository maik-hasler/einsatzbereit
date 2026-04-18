using Application.Common.Persistence;
using Application.Organizations.UpdateOrganization.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

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

    [Fact]
    public async Task Handle_ShouldUpdateOrganization_WithAllFields()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var orgId = Guid.NewGuid();
        var org = Organization.Create(new OrganizationId(orgId), "Alter Name");

        _orgRepo.FindAsync(new OrganizationId(orgId), ct).Returns(org);

        var command = new UpdateOrganizationCommand(
            orgId,
            "Neuer Name",
            "Eine Beschreibung",
            "kontakt@test.de",
            "+49 123 456",
            "https://example.org",
            new UpdateAddressCommand("Hauptstraße", "1", "12345", "Berlin"));

        // Act
        var result = await _sut.Handle(command, ct);

        // Assert
        result.Should().BeTrue();
        org.Name.Should().Be("Neuer Name");
        org.Description.Should().Be("Eine Beschreibung");
        org.ContactEmail.Should().Be("kontakt@test.de");
        org.ContactPhone.Should().Be("+49 123 456");
        org.Website.Should().Be("https://example.org");
        org.Address.Should().NotBeNull();
        org.Address!.Street.Should().Be("Hauptstraße");
        org.Address.City.Should().Be("Berlin");
    }

    [Fact]
    public async Task Handle_ShouldClearOptionalFields_WhenNullProvided()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var orgId = Guid.NewGuid();
        var org = Organization.Create(new OrganizationId(orgId), "Org");

        _orgRepo.FindAsync(new OrganizationId(orgId), ct).Returns(org);

        var command = new UpdateOrganizationCommand(
            orgId, "Org", null, null, null, null, null);

        // Act
        await _sut.Handle(command, ct);

        // Assert
        org.Description.Should().BeNull();
        org.ContactEmail.Should().BeNull();
        org.Address.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenOrganizationNotFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var orgId = Guid.NewGuid();

        _orgRepo.FindAsync(new OrganizationId(orgId), ct).Returns((Organization?)null);

        var command = new UpdateOrganizationCommand(
            orgId, "Name", null, null, null, null, null);

        // Act
        Func<Task> act = async () => await _sut.Handle(command, ct);

        // Assert
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenNameIsEmpty()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var orgId = Guid.NewGuid();
        var org = Organization.Create(new OrganizationId(orgId), "Org");

        _orgRepo.FindAsync(new OrganizationId(orgId), ct).Returns(org);

        var command = new UpdateOrganizationCommand(
            orgId, "   ", null, null, null, null, null);

        // Act
        Func<Task> act = async () => await _sut.Handle(command, ct);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Name must not be empty*");
    }
}
