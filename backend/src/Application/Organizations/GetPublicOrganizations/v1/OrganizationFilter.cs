namespace Application.Organizations.GetPublicOrganizations.v1;

public sealed record OrganizationFilter(
	int PageNumber,
	int PageSize,
	string? Search = null);
