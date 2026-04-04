using Domain.Organizations;
using Domain.VolunteerOpportunities;

namespace Application.Common.Persistence;

public interface IApplicationDbContext
{
    IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> VolunteerOpportunities { get; }

    IQueryable<VolunteerOpportunity> VolunteerOpportunitiesQuery { get; }

    IAggregateRepository<Organization, OrganizationId> Organizations { get; }

    IQueryable<Organization> OrganizationsQuery { get; }
}
