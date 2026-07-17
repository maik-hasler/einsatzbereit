using Application.Common.Messaging;
using Domain.Organizations;

namespace Application.Organizations.CreateOrganization.v1;

public sealed record CreateOrganizationCommand(
	string Name,
	Guid UserId,
	string? Description,
	string? ContactEmail,
	string? ContactPhone,
	string? Website,
	CreateAddressCommand? Address)
	: ICommand<Organization>;

public sealed record CreateAddressCommand(
	string Street,
	string HouseNumber,
	string ZipCode,
	string City);
