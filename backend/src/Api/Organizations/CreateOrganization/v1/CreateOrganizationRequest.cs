using System.ComponentModel.DataAnnotations;

namespace Api.Organizations.CreateOrganization.v1;

public sealed record CreateOrganizationRequest(
	[MaxLength(100)] string Name,
	[MaxLength(1000)] string? Description,
	[MaxLength(254)] string? ContactEmail,
	[MaxLength(30)] string? ContactPhone,
	[MaxLength(500)] string? Website,
	CreateAddressRequest? Address);

public sealed record CreateAddressRequest(
	[MaxLength(200)] string Street,
	[MaxLength(20)] string HouseNumber,
	[MaxLength(10)] string ZipCode,
	[MaxLength(100)] string City);
