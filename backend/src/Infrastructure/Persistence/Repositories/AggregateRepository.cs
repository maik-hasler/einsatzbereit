using System.Linq.Expressions;
using Application.Common.Persistence;
using Domain.Primitives;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class AggregateRepository<T, TKey>(
    DbSet<T> dbSet,
    IQueryable<T> aggregateQuery,
    Expression<Func<T, TKey>> keySelector)
    : IAggregateRepository<T, TKey>
    where T : AggregateRoot<TKey>
    where TKey : struct
{
    public async ValueTask<T?> FindAsync(
        TKey id,
        CancellationToken cancellationToken = default) =>
            await aggregateQuery.FirstOrDefaultAsync(
                BuildKeyPredicate(id),
                cancellationToken);
    
    public async ValueTask AddAsync(
        T entity,
        CancellationToken cancellationToken = default) =>
            await dbSet.AddAsync(entity, cancellationToken);
    
    public void Delete(T entity) =>
        dbSet.Remove(entity);
    
    private Expression<Func<T, bool>> BuildKeyPredicate(TKey id)
    {
        var match = Expression.Equal(keySelector.Body, Expression.Constant(id));
        
        return Expression.Lambda<Func<T, bool>>(match, keySelector.Parameters);
    }
}