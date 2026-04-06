using Application.Common.Messaging;
using Domain.Organizations;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.CreateVolunteerOpportunity.v1;

public sealed record CreateVolunteerOpportunityCommand(
    string Title,
    string Description,
    OrganizationId OrganizationId,
    bool IsRemote,
    Address? Address,
    Occurrence Occurrence,
    ParticipationType ParticipationType)
    : ICommand<VolunteerOpportunity>;
