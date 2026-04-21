namespace Api.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;

public sealed record UpdateVolunteerOpportunityRequest(
    string Title,
    string Description,
    bool IsRemote,
    string? Street,
    string? HouseNumber,
    string? ZipCode,
    string? City);
