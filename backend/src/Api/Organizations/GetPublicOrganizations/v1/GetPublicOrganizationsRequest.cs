namespace Api.Organizations.GetPublicOrganizations.v1;

public sealed record GetPublicOrganizationsRequest(
	int PageNumber,
	int PageSize,
	string? Search);
