using System.ComponentModel.DataAnnotations;

namespace Api.Organizations.UpdateOrganization.v1;

public sealed record UpdateOrganizationRequest(
	[MaxLength(100)] string Name,
	[MaxLength(1000)] string? Description,
	[MaxLength(254)] string? ContactEmail,
	[MaxLength(30)] string? ContactPhone,
	[MaxLength(500)] string? Website,
	UpdateAddressRequest? Address);

public sealed record UpdateAddressRequest(
	[MaxLength(200)] string Street,
	[MaxLength(20)] string HouseNumber,
	[MaxLength(10)] string ZipCode,
	[MaxLength(100)] string City);
