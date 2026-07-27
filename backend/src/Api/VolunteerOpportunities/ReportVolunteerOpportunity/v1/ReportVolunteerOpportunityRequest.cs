using System.ComponentModel.DataAnnotations;

namespace Api.VolunteerOpportunities.ReportVolunteerOpportunity.v1;

public sealed record ReportVolunteerOpportunityRequest(
	string Reason,
	[MaxLength(1000)] string? Details);
