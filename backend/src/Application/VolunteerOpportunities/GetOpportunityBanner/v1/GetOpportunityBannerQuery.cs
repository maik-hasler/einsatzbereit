using Application.Common.Messaging;

namespace Application.VolunteerOpportunities.GetOpportunityBanner.v1;

public sealed record GetOpportunityBannerQuery(
	Guid OpportunityId)
	: IQuery<OpportunityBannerDto?>;

public sealed record OpportunityBannerDto(
	byte[] Content,
	string ContentType);
