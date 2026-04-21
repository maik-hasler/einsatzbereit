using Application.Common.Messaging;

namespace Application.VolunteerOpportunities.DeleteVolunteerOpportunity.v1;

public sealed record DeleteVolunteerOpportunityCommand(Guid OpportunityId)
    : ICommand<bool>;
