using Domain.Primitives;

namespace Domain.ValueObjects;

public sealed class Address
    : ValueObject
{
    public Address(string street, string city, string state, string country, string zipCode)
    {
        Street = street;
        City = city;
        State = state;
        Country = country;
        ZipCode = zipCode;
    }

    public string Street { get; private init; }
    
    public string City { get; private init; }
    
    public string State { get; private init; }
    
    public string Country { get; private init; }
    
    public string ZipCode { get; private init; }
    
    protected override IEnumerable<object> GetAtomicValues()
    {
        throw new NotImplementedException();
    }
}