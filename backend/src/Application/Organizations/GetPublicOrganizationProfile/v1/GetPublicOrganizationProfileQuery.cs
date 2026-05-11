using Application.Common.Messaging;

namespace Application.Organizations.GetPublicOrganizationProfile.v1;

public sealed record GetPublicOrganizationProfileQuery(Guid OrganizationId)
	: IQuery<PublicOrganizationProfileResponse?>;
