namespace Application.Organizations.ListOrganizations.v1;

public sealed record AdminOrganizationSummary(
	Guid Id,
	string Name,
	string? LogoUrl);
