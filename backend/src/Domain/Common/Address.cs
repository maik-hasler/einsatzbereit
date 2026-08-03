using Domain.Primitives;

namespace Domain.Common;

public sealed record Address : IValueObject
{
	public string Street { get; }
	public string HouseNumber { get; }
	public string ZipCode { get; }
	public string City { get; }
	public double? Latitude { get; }
	public double? Longitude { get; }

	// A second, narrower constructor (rather than default parameter values) so
	// EF Core's owned-type materialization can bind it for an owner
	// (Organization, #1206) that deliberately doesn't map Latitude/Longitude -
	// EF picks whichever constructor's parameters are all bindable to that
	// owner's mapped properties. Create/WithCoordinates below always call the
	// 6-arg constructor explicitly regardless.
	private Address(string street, string houseNumber, string zipCode, string city)
		: this(street, houseNumber, zipCode, city, latitude: null, longitude: null)
	{
	}

	private Address(string street, string houseNumber, string zipCode, string city, double? latitude, double? longitude)
	{
		Street = street;
		HouseNumber = houseNumber;
		ZipCode = zipCode;
		City = city;
		Latitude = latitude;
		Longitude = longitude;
	}

	public static Result<Address> Create(string street, string houseNumber, string zipCode, string city) =>
		Create(street, houseNumber, zipCode, city, latitude: null, longitude: null);

	private static Result<Address> Create(string street, string houseNumber, string zipCode, string city, double? latitude, double? longitude)
	{
		if (string.IsNullOrWhiteSpace(street))
			return Result.Failure<Address>(Error.Validation("Address.StreetRequired", "Street must not be empty."));

		if (string.IsNullOrWhiteSpace(houseNumber))
			return Result.Failure<Address>(Error.Validation("Address.HouseNumberRequired", "House number must not be empty."));

		if (string.IsNullOrWhiteSpace(zipCode) || zipCode.Length != 5)
			return Result.Failure<Address>(Error.Validation("Address.ZipCodeInvalid", "Zip code must be exactly 5 characters."));

		if (string.IsNullOrWhiteSpace(city))
			return Result.Failure<Address>(Error.Validation("Address.CityRequired", "City must not be empty."));

		if (latitude is < -90 or > 90)
			return Result.Failure<Address>(Error.Validation("Address.LatitudeOutOfRange", "Latitude must be between -90 and 90."));

		if (longitude is < -180 or > 180)
			return Result.Failure<Address>(Error.Validation("Address.LongitudeOutOfRange", "Longitude must be between -180 and 180."));

		return new Address(street, houseNumber, zipCode, city, latitude, longitude);
	}

	public Result<Address> WithCoordinates(double latitude, double longitude) =>
		Create(Street, HouseNumber, ZipCode, City, latitude, longitude);
}
