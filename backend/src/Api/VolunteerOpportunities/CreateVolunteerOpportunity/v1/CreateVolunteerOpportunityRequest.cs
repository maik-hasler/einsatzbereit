namespace Api.VolunteerOpportunities.CreateVolunteerOpportunity.v1;

public sealed record CreateVolunteerOpportunityRequest(
    string Title,
    string Description,
    Guid OrganizationId,
    string Street,
    string HouseNumber,
    string ZipCode,
    string City,
    string Occurrence,
    string ParticipationType);
