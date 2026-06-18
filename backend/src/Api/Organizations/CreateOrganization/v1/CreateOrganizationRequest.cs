using System.ComponentModel.DataAnnotations;

namespace Api.Organizations.CreateOrganization.v1;

public sealed record CreateOrganizationRequest(
	[MaxLength(100)] string Name);
