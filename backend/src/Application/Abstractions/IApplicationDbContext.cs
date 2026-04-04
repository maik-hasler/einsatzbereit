using Domain.VolunteerOpportunities;
using Domain.Organizations;

namespace Application.Abstractions;

public interface IApplicationDbContext
{
    IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> VolunteerOpportunities { get; }

    IQueryable<VolunteerOpportunity> VolunteerOpportunitiesQuery { get; }

    IAggregateRepository<Organization, OrganizationId> Organizations { get; }

    IQueryable<Organization> OrganizationsQuery { get; }
}
