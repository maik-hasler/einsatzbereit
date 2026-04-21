using Application.Common.Messaging;

namespace Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;

internal sealed class GetVolunteerOpportunityDetailsQueryHandler(
    IVolunteerOpportunityReadRepository readRepository)
    : IQueryHandler<GetVolunteerOpportunityDetailsQuery, VolunteerOpportunityDetails?>
{
    public async ValueTask<VolunteerOpportunityDetails?> Handle(
        GetVolunteerOpportunityDetailsQuery request,
        CancellationToken cancellationToken = default) =>
            await readRepository.GetDetailsAsync(request.OpportunityId, cancellationToken);
}
