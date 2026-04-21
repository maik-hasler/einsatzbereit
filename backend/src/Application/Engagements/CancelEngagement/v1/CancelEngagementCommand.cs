using Application.Common.Messaging;
using Domain.Engagements;

namespace Application.Engagements.CancelEngagement.v1;

public sealed record CancelEngagementCommand(EngagementId EngagementId)
    : ICommand<Engagement>;
