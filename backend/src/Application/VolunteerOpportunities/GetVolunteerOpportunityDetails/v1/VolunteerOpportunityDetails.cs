namespace Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;

public sealed record VolunteerOpportunityDetails(
    Guid Id,
    string Title,
    string Description,
    Guid OrganizationId,
    string OrganizationName,
    string? Street,
    string? HouseNumber,
    string? ZipCode,
    string? City,
    bool IsRemote,
    string Occurrence,
    string ParticipationType,
    IReadOnlyList<TimeSlotDetail> TimeSlots,
    DateTimeOffset CreatedOn);

public sealed record TimeSlotDetail(
    Guid Id,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    int MaxParticipants);
