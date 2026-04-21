using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Primitives;

namespace Application.Engagements.CancelEngagement.v1;

internal sealed class CancelEngagementCommandHandler(
    IApplicationDbContext dbContext)
    : ICommandHandler<CancelEngagementCommand, Engagement>
{
    public async ValueTask<Engagement> Handle(
        CancelEngagementCommand request,
        CancellationToken cancellationToken = default)
    {
        var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
            ?? throw new DomainException($"Engagement '{request.EngagementId.Value}' not found.");

        engagement.Cancel();

        return engagement;
    }
}
