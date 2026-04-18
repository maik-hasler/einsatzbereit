using Application.Common.Messaging;

namespace Application.Organizations.GetOrganizationDetails.v1;

public sealed record GetOrganizationDetailsQuery(
    Guid OrganizationId)
    : IQuery<OrganizationDetailsResponse?>;
