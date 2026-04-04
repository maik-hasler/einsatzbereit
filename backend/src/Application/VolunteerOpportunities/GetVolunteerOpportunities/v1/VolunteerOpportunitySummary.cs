namespace Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

public sealed record VolunteerOpportunitySummary(
    Guid Id,
    string Title,
    string Description,
    string OrganizationName,
    string? Street,
    string? HouseNumber,
    string? ZipCode,
    string? City,
    bool IsRemote,
    string Occurrence,
    string ParticipationType,
    DateTimeOffset CreatedOn);
