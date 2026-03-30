using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed record Address
{
    public string Street { get; }
    public string HouseNumber { get; }
    public string ZipCode { get; }
    public string City { get; }

    public Address(string street, string houseNumber, string zipCode, string city)
    {
        if (string.IsNullOrWhiteSpace(street))
            throw new DomainException("Street must not be empty.");

        if (string.IsNullOrWhiteSpace(houseNumber))
            throw new DomainException("House number must not be empty.");

        if (string.IsNullOrWhiteSpace(zipCode) || zipCode.Length != 5)
            throw new DomainException("Zip code must be exactly 5 characters.");

        if (string.IsNullOrWhiteSpace(city))
            throw new DomainException("City must not be empty.");

        Street = street;
        HouseNumber = houseNumber;
        ZipCode = zipCode;
        City = city;
    }
}
