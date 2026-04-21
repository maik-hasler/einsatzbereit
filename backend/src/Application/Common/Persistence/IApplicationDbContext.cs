using Domain.Engagements;
using Domain.Organizations;
using Domain.VolunteerOpportunities;

namespace Application.Common.Persistence;

public interface IApplicationDbContext
{
    IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> VolunteerOpportunities { get; }

    IAggregateRepository<Organization, OrganizationId> Organizations { get; }

    IAggregateRepository<Engagement, EngagementId> Engagements { get; }
}
