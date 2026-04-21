using Application.Common.Messaging;
using Domain.Users;

namespace Application.Engagements.GetMyEngagements.v1;

public sealed record GetMyEngagementsQuery(UserId VolunteerId)
    : IQuery<List<EngagementSummary>>;
