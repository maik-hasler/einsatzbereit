using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.CreateVolunteerOpportunity.v1;

internal sealed class CreateVolunteerOpportunityCommandHandler(
    IApplicationDbContext dbContext)
    : ICommandHandler<CreateVolunteerOpportunityCommand, VolunteerOpportunity>
{
    public async ValueTask<VolunteerOpportunity> Handle(
        CreateVolunteerOpportunityCommand request,
        CancellationToken cancellationToken = default)
    {
        var opportunity = VolunteerOpportunity.Create(
            request.OrganizationId,
            request.Title,
            request.Description,
            request.Location,
            request.Occurrence,
            request.ParticipationType);

        await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);

        return opportunity;
    }
}
