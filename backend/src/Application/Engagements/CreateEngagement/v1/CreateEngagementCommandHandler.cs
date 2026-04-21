using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Primitives;

namespace Application.Engagements.CreateEngagement.v1;

internal sealed class CreateEngagementCommandHandler(
    IApplicationDbContext dbContext)
    : ICommandHandler<CreateEngagementCommand, Engagement>
{
    public async ValueTask<Engagement> Handle(
        CreateEngagementCommand request,
        CancellationToken cancellationToken = default)
    {
        var engagement = request.TimeSlotId is not null
            ? Engagement.CreateWaitlistSignUp(request.OpportunityId, request.VolunteerId, request.TimeSlotId.Value)
            : Engagement.CreateIndividualContact(request.OpportunityId, request.VolunteerId, request.Message
                ?? throw new DomainException("Message is required for individual contact."));

        await dbContext.Engagements.AddAsync(engagement, cancellationToken);

        return engagement;
    }
}
