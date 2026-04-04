using Microsoft.EntityFrameworkCore;

namespace Application.Pagination;

internal static class PagedListExtensions
{
    public static async ValueTask<PagedList<T>> ToPagedListAsync<T>(
        this IQueryable<T> source,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var count = await source.CountAsync(cancellationToken);
        
        var items = await source
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        
        return new PagedList<T>(items, count, pageNumber, pageSize);
    }
}