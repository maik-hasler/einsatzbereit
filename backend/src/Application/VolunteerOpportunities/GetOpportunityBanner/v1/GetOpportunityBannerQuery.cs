using Application.Common.Messaging;

namespace Application.VolunteerOpportunities.GetOpportunityBanner.v1;

public sealed record GetOpportunityBannerQuery(
	Guid OpportunityId)
	: IQuery<string?>;
