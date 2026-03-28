using Domain.Organisationen;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class OrganisationRepository(
    ApplicationDbContext dbContext)
    : IOrganisationRepository
{
    public async ValueTask AddAsync(
        Organisation organisation,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Organisationen.AddAsync(organisation, cancellationToken);
    }

    public async ValueTask<Organisation?> GetByKeycloakIdAsync(
        Guid keycloakId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext
            .Organisationen
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.KeycloakId == keycloakId, cancellationToken);
    }
}
