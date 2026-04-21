namespace Api.VolunteerOpportunities.GetVolunteerOpportunities.v1;

public sealed record GetVolunteerOpportunitiesRequest(
    int PageNumber,
    int PageSize,
    string? Search,
    string? City,
    string? Occurrence,
    string? ParticipationType);
