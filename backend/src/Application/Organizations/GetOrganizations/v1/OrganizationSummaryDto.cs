namespace Application.Organizations.GetOrganizations.v1;

public sealed record OrganizationSummaryDto(
	Guid Id,
	string Name,
	string? Slug);
