using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Primitives;

namespace Application.Engagements.WithdrawEngagement.v1;

internal sealed class WithdrawEngagementCommandHandler(
    IApplicationDbContext dbContext)
    : ICommandHandler<WithdrawEngagementCommand, Engagement>
{
    public async ValueTask<Engagement> Handle(
        WithdrawEngagementCommand request,
        CancellationToken cancellationToken = default)
    {
        var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
            ?? throw new DomainException($"Engagement '{request.EngagementId.Value}' not found.");

        if (engagement.VolunteerId.Value != request.VolunteerId)
            throw new DomainException("Only the volunteer who created this engagement can withdraw it.");

        engagement.Withdraw();

        return engagement;
    }
}
