using Domain.Primitives;

namespace Domain.Bedarfe;

public interface IBedarfRepository
{
    ValueTask<PagedList<Bedarf>> GetAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
    
    ValueTask AddAsync(
        Bedarf bedarf,
        CancellationToken cancellationToken = default);
}