using Application.Common.Messaging;

namespace Application.Engagements.GetMyEngagements.v1;

internal sealed class GetMyEngagementsQueryHandler(
    IEngagementReadRepository readRepository)
    : IQueryHandler<GetMyEngagementsQuery, List<EngagementSummary>>
{
    public async ValueTask<List<EngagementSummary>> Handle(
        GetMyEngagementsQuery request,
        CancellationToken cancellationToken = default) =>
            await readRepository.GetByVolunteerAsync(request.VolunteerId, cancellationToken);
}
