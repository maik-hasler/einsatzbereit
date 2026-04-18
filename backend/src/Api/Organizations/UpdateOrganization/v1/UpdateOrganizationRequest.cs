namespace Api.Organizations.UpdateOrganization.v1;

public sealed record UpdateOrganizationRequest(
    string Name,
    string? Description,
    string? ContactEmail,
    string? ContactPhone,
    string? Website,
    UpdateAddressRequest? Address);

public sealed record UpdateAddressRequest(
    string Street,
    string HouseNumber,
    string ZipCode,
    string City);
