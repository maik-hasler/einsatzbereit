using Application.Common.Messaging;

namespace Application.VolunteerOpportunities.GetOpportunityBanner.v1;

internal sealed class GetOpportunityBannerQueryHandler(
	IVolunteerOpportunityReadRepository readRepository)
	: IQueryHandler<GetOpportunityBannerQuery, string?>
{
	public async ValueTask<string?> Handle(
		GetOpportunityBannerQuery request,
		CancellationToken cancellationToken = default) =>
		await readRepository.GetBannerUrlAsync(request.OpportunityId, cancellationToken);
}
