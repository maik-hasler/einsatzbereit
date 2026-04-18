using Application.Common.Messaging;

namespace Application.Organizations.UpdateOrganization.v1;

public sealed record UpdateOrganizationCommand(
    Guid OrganizationId,
    string Name,
    string? Description,
    string? ContactEmail,
    string? ContactPhone,
    string? Website,
    UpdateAddressCommand? Address)
    : ICommand<bool>;

public sealed record UpdateAddressCommand(
    string Street,
    string HouseNumber,
    string ZipCode,
    string City);
