namespace Application.Organizations.GetOrganizationDetails.v1;

public sealed record OrganizationDetailsResponse(
    Guid Id,
    string Name,
    string? Description,
    string? ContactEmail,
    string? ContactPhone,
    string? Website,
    AddressDto? Address,
    DateTimeOffset CreatedOn,
    DateTimeOffset? ModifiedOn,
    IReadOnlyList<OrganizationMemberDto> Members);

public sealed record AddressDto(
    string Street,
    string HouseNumber,
    string ZipCode,
    string City);

public sealed record OrganizationMemberDto(
    Guid UserId,
    string Username,
    string? FirstName,
    string? LastName,
    string Email,
    bool IsOrganisator);
