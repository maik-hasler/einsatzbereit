using Application.Common.Messaging;

namespace Application.Engagements.GetEngagements.v1;

internal sealed class GetEngagementsQueryHandler(
    IEngagementReadRepository readRepository)
    : IQueryHandler<GetEngagementsQuery, List<EngagementSummary>>
{
    public async ValueTask<List<EngagementSummary>> Handle(
        GetEngagementsQuery request,
        CancellationToken cancellationToken = default) =>
            await readRepository.GetByOpportunityAsync(request.OpportunityId, cancellationToken);
}
