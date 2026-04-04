using Application.Abstractions;
using Application.Messaging;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.CreateVolunteerOpportunity.v1;

internal sealed class CreateVolunteerOpportunityCommandHandler(
    IApplicationDbContext dbContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateVolunteerOpportunityCommand, VolunteerOpportunity>
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

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return opportunity;
    }
}
