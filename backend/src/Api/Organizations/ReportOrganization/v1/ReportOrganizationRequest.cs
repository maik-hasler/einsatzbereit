using System.ComponentModel.DataAnnotations;

namespace Api.Organizations.ReportOrganization.v1;

public sealed record ReportOrganizationRequest(
	string Reason,
	[MaxLength(1000)] string? Details);
