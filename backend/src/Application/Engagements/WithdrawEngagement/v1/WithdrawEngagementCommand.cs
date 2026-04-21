using Application.Common.Messaging;
using Domain.Engagements;

namespace Application.Engagements.WithdrawEngagement.v1;

public sealed record WithdrawEngagementCommand(EngagementId EngagementId, Guid VolunteerId)
    : ICommand<Engagement>;
