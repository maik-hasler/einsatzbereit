using Domain.Bedarfe;
using Domain.Primitives;
using Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class BedarfRepository(
    ApplicationDbContext dbContext)
    : IBedarfRepository
{
    public async ValueTask<PagedList<Bedarf>> GetAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await dbContext
            .Bedarfe
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedOn)
            .ToPagedListAsync(pageNumber, pageSize, cancellationToken);
    }

    public async ValueTask AddAsync(
        Bedarf bedarf,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Bedarfe.AddAsync(bedarf, cancellationToken);
    }
}