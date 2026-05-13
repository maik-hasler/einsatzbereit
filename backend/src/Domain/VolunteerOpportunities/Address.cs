using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed record Address
{
	public string Street { get; }
	public string HouseNumber { get; }
	public string ZipCode { get; }
	public string City { get; }
	public double? Latitude { get; }
	public double? Longitude { get; }

	public Address(string street, string houseNumber, string zipCode, string city)
		: this(street, houseNumber, zipCode, city, null, null)
	{
	}

	private Address(string street, string houseNumber, string zipCode, string city, double? latitude, double? longitude)
	{
		if (string.IsNullOrWhiteSpace(street))
			throw new DomainException("Street must not be empty.");

		if (string.IsNullOrWhiteSpace(houseNumber))
			throw new DomainException("House number must not be empty.");

		if (string.IsNullOrWhiteSpace(zipCode) || zipCode.Length != 5)
			throw new DomainException("Zip code must be exactly 5 characters.");

		if (string.IsNullOrWhiteSpace(city))
			throw new DomainException("City must not be empty.");

		if (latitude is < -90 or > 90)
			throw new DomainException("Latitude must be between -90 and 90.");

		if (longitude is < -180 or > 180)
			throw new DomainException("Longitude must be between -180 and 180.");

		Street = street;
		HouseNumber = houseNumber;
		ZipCode = zipCode;
		City = city;
		Latitude = latitude;
		Longitude = longitude;
	}

	public Address WithCoordinates(double latitude, double longitude) =>
		new(Street, HouseNumber, ZipCode, City, latitude, longitude);
}
