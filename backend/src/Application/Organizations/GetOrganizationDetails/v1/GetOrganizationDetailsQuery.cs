using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.GetOrganizationDetails.v1;

public sealed record GetOrganizationDetailsQuery(
	string OrganizationIdOrSlug,
	UserId RequestingUserId)
	: IQuery<OrganizationDetailsResponse?>;
