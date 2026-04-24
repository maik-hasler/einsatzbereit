using AwesomeAssertions;
using Domain.Primitives;
using Domain.VolunteerOpportunities;


namespace Application.UnitTests.VolunteerOpportunities;

public class AddressTests
{
    [Test]
    public void Constructor_ShouldCreateAddress_WithValidData()
    {
        // Act
        var address = new Address("Musterstraße", "42a", "12345", "Berlin");

        // Assert
        address.Street.Should().Be("Musterstraße");
        address.HouseNumber.Should().Be("42a");
        address.ZipCode.Should().Be("12345");
        address.City.Should().Be("Berlin");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments(null)]
    public void Constructor_ShouldThrow_WhenStreetIsEmpty(string? street)
    {
        var act = () => new Address(street!, "1", "12345", "Berlin");

        act.Should().Throw<DomainException>()
            .WithMessage("Street must not be empty.");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments(null)]
    public void Constructor_ShouldThrow_WhenHouseNumberIsEmpty(string? houseNumber)
    {
        var act = () => new Address("Straße", houseNumber!, "12345", "Berlin");

        act.Should().Throw<DomainException>()
            .WithMessage("House number must not be empty.");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments(null)]
    [Arguments("1234")]
    [Arguments("123456")]
    public void Constructor_ShouldThrow_WhenZipCodeIsInvalid(string? zipCode)
    {
        var act = () => new Address("Straße", "1", zipCode!, "Berlin");

        act.Should().Throw<DomainException>()
            .WithMessage("Zip code must be exactly 5 characters.");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments(null)]
    public void Constructor_ShouldThrow_WhenCityIsEmpty(string? city)
    {
        var act = () => new Address("Straße", "1", "12345", city!);

        act.Should().Throw<DomainException>()
            .WithMessage("City must not be empty.");
    }

    [Test]
    public void Equals_ShouldReturnTrue_ForSameValues()
    {
        var address1 = new Address("Straße", "1", "12345", "Berlin");
        var address2 = new Address("Straße", "1", "12345", "Berlin");

        address1.Should().Be(address2);
    }
}
