using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.DeleteVolunteerOpportunity.v1;

internal sealed class DeleteVolunteerOpportunityCommandHandler(
    IApplicationDbContext dbContext)
    : ICommandHandler<DeleteVolunteerOpportunityCommand, bool>
{
    public async ValueTask<bool> Handle(
        DeleteVolunteerOpportunityCommand request,
        CancellationToken cancellationToken = default)
    {
        var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
            new VolunteerOpportunityId(request.OpportunityId), cancellationToken)
            ?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId}' not found.");

        dbContext.VolunteerOpportunities.Delete(opportunity);

        return true;
    }
}
