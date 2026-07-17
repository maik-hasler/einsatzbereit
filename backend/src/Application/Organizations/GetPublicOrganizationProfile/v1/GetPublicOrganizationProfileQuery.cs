using Application.Common.Messaging;

namespace Application.Organizations.GetPublicOrganizationProfile.v1;

public sealed record GetPublicOrganizationProfileQuery(string OrganizationIdOrSlug)
	: IQuery<PublicOrganizationProfileResponse?>;
