namespace Application.Common.Pagination;

public sealed class PagedList<T>(
    IReadOnlyCollection<T> items,
    int count,
    int currentPage,
    int pageSize)
{
    public int TotalItems { get; set; } = count;

    public int CurrentPage { get; set; } = currentPage;

    public int PageCount { get; set; } = (int)Math.Ceiling(count / (double)pageSize);

    public IReadOnlyCollection<T> Items { get; set; } = items;

    public PagedList<TResult> Map<TResult>(Func<T, TResult> selector) =>
        new(Items.Select(selector).ToList(), TotalItems, CurrentPage, pageSize);
}