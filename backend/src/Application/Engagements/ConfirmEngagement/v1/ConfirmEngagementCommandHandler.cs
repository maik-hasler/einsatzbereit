using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Primitives;

namespace Application.Engagements.ConfirmEngagement.v1;

internal sealed class ConfirmEngagementCommandHandler(
    IApplicationDbContext dbContext)
    : ICommandHandler<ConfirmEngagementCommand, Engagement>
{
    public async ValueTask<Engagement> Handle(
        ConfirmEngagementCommand request,
        CancellationToken cancellationToken = default)
    {
        var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
            ?? throw new DomainException($"Engagement '{request.EngagementId.Value}' not found.");

        engagement.Confirm();

        return engagement;
    }
}
