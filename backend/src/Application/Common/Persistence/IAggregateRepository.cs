using Domain.Primitives;

namespace Application.Common.Persistence;

public interface IAggregateRepository<T, in TKey>
    where T : AggregateRoot<TKey>
    where TKey : struct
{
    ValueTask<T?> FindAsync(
        TKey id,
        CancellationToken cancellationToken = default);

    ValueTask AddAsync(
        T entity,
        CancellationToken cancellationToken = default);

    void Delete(T entity);
}