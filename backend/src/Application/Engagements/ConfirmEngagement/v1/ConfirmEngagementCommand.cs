using Application.Common.Messaging;
using Domain.Engagements;

namespace Application.Engagements.ConfirmEngagement.v1;

public sealed record ConfirmEngagementCommand(EngagementId EngagementId)
    : ICommand<Engagement>;
