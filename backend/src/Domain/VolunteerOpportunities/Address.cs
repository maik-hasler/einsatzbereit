namespace Domain.VolunteerOpportunities;

public sealed record Address(
    string Street,
    string Housenumber,
    string ZipCode,
    string City,
    string Country);