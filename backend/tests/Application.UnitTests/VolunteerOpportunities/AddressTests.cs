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
		var address = new Address("Sample Street", "42a", "12345", "Berlin");

		// Assert
		address.Street.Should().Be("Sample Street");
		address.HouseNumber.Should().Be("42a");
		address.ZipCode.Should().Be("12345");
		address.City.Should().Be("Berlin");
		address.Latitude.Should().BeNull();
		address.Longitude.Should().BeNull();
	}

	[Test]
	public void WithCoordinates_ShouldSetCoordinates_AndPreserveOtherFields()
	{
		var address = new Address("Sample Street", "42a", "12345", "Berlin");

		var located = address.WithCoordinates(52.52, 13.405);

		located.Street.Should().Be("Sample Street");
		located.HouseNumber.Should().Be("42a");
		located.ZipCode.Should().Be("12345");
		located.City.Should().Be("Berlin");
		located.Latitude.Should().Be(52.52);
		located.Longitude.Should().Be(13.405);
	}

	[Test]
	[Arguments(-90.1, 0.0)]
	[Arguments(90.1, 0.0)]
	[Arguments(0.0, -180.1)]
	[Arguments(0.0, 180.1)]
	public void WithCoordinates_ShouldThrow_WhenOutOfRange(double latitude, double longitude)
	{
		var address = new Address("Sample Street", "1", "12345", "Berlin");

		var act = () => address.WithCoordinates(latitude, longitude);

		act.Should().Throw<DomainException>();
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
		var act = () => new Address("Test Street", houseNumber!, "12345", "Berlin");

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
		var act = () => new Address("Test Street", "1", zipCode!, "Berlin");

		act.Should().Throw<DomainException>()
			.WithMessage("Zip code must be exactly 5 characters.");
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Constructor_ShouldThrow_WhenCityIsEmpty(string? city)
	{
		var act = () => new Address("Test Street", "1", "12345", city!);

		act.Should().Throw<DomainException>()
			.WithMessage("City must not be empty.");
	}

	[Test]
	public void Equals_ShouldReturnTrue_ForSameValues()
	{
		var address1 = new Address("Test Street", "1", "12345", "Berlin");
		var address2 = new Address("Test Street", "1", "12345", "Berlin");

		address1.Should().Be(address2);
	}
}
