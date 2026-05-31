using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.UpdateOrganization.v1;

public sealed record UpdateOrganizationCommand(
	Guid OrganizationId,
	string Name,
	string? Description,
	string? ContactEmail,
	string? ContactPhone,
	string? Website,
	UpdateAddressCommand? Address,
	UserId RequestingUserId)
	: ICommand<bool>;

public sealed record UpdateAddressCommand(
	string Street,
	string HouseNumber,
	string ZipCode,
	string City);
