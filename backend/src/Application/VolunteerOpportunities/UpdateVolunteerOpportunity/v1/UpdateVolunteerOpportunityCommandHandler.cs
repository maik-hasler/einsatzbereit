using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;

internal sealed class UpdateVolunteerOpportunityCommandHandler(
    IApplicationDbContext dbContext)
    : ICommandHandler<UpdateVolunteerOpportunityCommand, bool>
{
    public async ValueTask<bool> Handle(
        UpdateVolunteerOpportunityCommand request,
        CancellationToken cancellationToken = default)
    {
        var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
            new VolunteerOpportunityId(request.OpportunityId), cancellationToken)
            ?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId}' not found.");

        opportunity.Update(request.Title, request.Description, request.IsRemote, request.Address);

        return true;
    }
}
