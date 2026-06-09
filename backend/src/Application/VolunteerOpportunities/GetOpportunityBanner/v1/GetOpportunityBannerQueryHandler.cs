using Application.Common.Messaging;

namespace Application.VolunteerOpportunities.GetOpportunityBanner.v1;

internal sealed class GetOpportunityBannerQueryHandler(
	IVolunteerOpportunityReadRepository readRepository)
	: IQueryHandler<GetOpportunityBannerQuery, OpportunityBannerDto?>
{
	public async ValueTask<OpportunityBannerDto?> Handle(
		GetOpportunityBannerQuery request,
		CancellationToken cancellationToken = default) =>
		await readRepository.GetBannerAsync(request.OpportunityId, cancellationToken);
}
