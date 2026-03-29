using Domain.Bedarfe;
using Domain.Organisationen;

namespace Application.Abstractions;

public interface IApplicationDbContext
{
    IAggregateRepository<Bedarf, BedarfId> Bedarfe { get; }
    
    IQueryable<Bedarf> BedarfeQuery { get; }

    IAggregateRepository<Organisation, OrganisationId> Organisationen { get; }
    
    IQueryable<Organisation> OrganisationenQuery { get; }
}