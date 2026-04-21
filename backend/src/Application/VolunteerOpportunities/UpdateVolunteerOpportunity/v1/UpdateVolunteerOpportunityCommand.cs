using Application.Common.Messaging;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;

public sealed record UpdateVolunteerOpportunityCommand(
    Guid OpportunityId,
    string Title,
    string Description,
    bool IsRemote,
    Address? Address)
    : ICommand<bool>;
