using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.GetOrganizationDetails.v1;

public sealed record GetOrganizationDetailsQuery(
	Guid OrganizationId,
	UserId RequestingUserId)
	: IQuery<OrganizationDetailsResponse?>;
