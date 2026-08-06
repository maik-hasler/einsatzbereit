namespace Application.Organizations.ListOrganizations.v1;

public sealed record AdminOrganizationSummary(
	Guid Id,
	string Name,
	string? LogoUrl,
	bool IsDeleted,
	int OpenReportCount,
	int MemberCount,
	DateTimeOffset CreatedOn);
