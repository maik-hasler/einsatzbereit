using AwesomeAssertions;
using Domain.Common;

namespace Application.UnitTests.VolunteerOpportunities;

public class AddressTests
{
	[Test]
	public void Create_ShouldCreateAddress_WithValidData()
	{
		// Act
		var address = Address.Create("Sample Street", "42a", "12345", "Berlin").Value;

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
		var address = Address.Create("Sample Street", "42a", "12345", "Berlin").Value;

		var located = address.WithCoordinates(52.52, 13.405).Value;

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
	public void WithCoordinates_ShouldFail_WhenOutOfRange(double latitude, double longitude)
	{
		var address = Address.Create("Sample Street", "1", "12345", "Berlin").Value;

		var result = address.WithCoordinates(latitude, longitude);

		result.IsFailure.Should().BeTrue();
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Create_ShouldFail_WhenStreetIsEmpty(string? street)
	{
		var result = Address.Create(street!, "1", "12345", "Berlin");

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Street must not be empty.");
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Create_ShouldFail_WhenHouseNumberIsEmpty(string? houseNumber)
	{
		var result = Address.Create("Test Street", houseNumber!, "12345", "Berlin");

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("House number must not be empty.");
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	[Arguments("1234")]
	[Arguments("123456")]
	public void Create_ShouldFail_WhenZipCodeIsInvalid(string? zipCode)
	{
		var result = Address.Create("Test Street", "1", zipCode!, "Berlin");

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Zip code must be exactly 5 characters.");
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Create_ShouldFail_WhenCityIsEmpty(string? city)
	{
		var result = Address.Create("Test Street", "1", "12345", city!);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("City must not be empty.");
	}

	[Test]
	public void Equals_ShouldReturnTrue_ForSameValues()
	{
		var address1 = Address.Create("Test Street", "1", "12345", "Berlin").Value;
		var address2 = Address.Create("Test Street", "1", "12345", "Berlin").Value;

		address1.Should().Be(address2);
	}
}
