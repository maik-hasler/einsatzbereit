using System.ComponentModel.DataAnnotations;

namespace Api.VolunteerOpportunities.CancelVolunteerOpportunity.v1;

public sealed record CancelVolunteerOpportunityRequest([MaxLength(500)] string? Reason);
