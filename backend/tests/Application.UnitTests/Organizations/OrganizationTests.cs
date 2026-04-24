using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;


namespace Application.UnitTests.Organizations;

public class OrganizationTests
{
    [Test]
    public void Update_ShouldSetAllFields()
    {
        // Arrange
        var org = Organization.Create(new OrganizationId(Guid.NewGuid()), "Original");
        var address = new Address("Musterstraße", "1", "12345", "Berlin");

        // Act
        org.Update("Geändert", "Beschreibung", "mail@test.de", "+49 30 123", "https://test.de", address);

        // Assert
        org.Name.Should().Be("Geändert");
        org.Description.Should().Be("Beschreibung");
        org.ContactEmail.Should().Be("mail@test.de");
        org.ContactPhone.Should().Be("+49 30 123");
        org.Website.Should().Be("https://test.de");
        org.Address.Should().Be(address);
    }

    [Test]
    public void Update_ShouldClearAddress_WhenNullPassed()
    {
        // Arrange
        var org = Organization.Create(new OrganizationId(Guid.NewGuid()), "Org");
        org.Update("Org", null, null, null, null, new Address("Str", "1", "12345", "Stadt"));

        // Act
        org.Update("Org", null, null, null, null, null);

        // Assert
        org.Address.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Update_ShouldThrow_WhenNameIsEmpty(string? name)
    {
        // Arrange
        var org = Organization.Create(new OrganizationId(Guid.NewGuid()), "Org");

        // Act
        var act = () => org.Update(name!, null, null, null, null, null);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Name must not be empty.");
    }

    [Test]
    public void Create_ShouldThrow_WhenNameIsEmpty()
    {
        var act = () => Organization.Create(new OrganizationId(Guid.NewGuid()), "");
        act.Should().Throw<DomainException>();
    }
}
