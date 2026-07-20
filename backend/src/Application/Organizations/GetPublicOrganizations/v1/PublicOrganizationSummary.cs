namespace Application.Organizations.GetPublicOrganizations.v1;

public sealed record PublicOrganizationSummary(
	Guid Id,
	string Name,
	string? Description,
	string? City,
	string? LogoUrl,
	bool IsVerified,
	int OpenOpportunityCount);
