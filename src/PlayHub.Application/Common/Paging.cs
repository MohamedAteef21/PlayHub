namespace PlayHub.Application.Common;

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}

public static class PagingHelper
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize, int Skip) Normalize(
        int page,
        int pageSize,
        int defaultSize = DefaultPageSize,
        int maxSize = MaxPageSize)
    {
        var p = page < 1 ? 1 : page;
        var size = pageSize < 1 ? defaultSize : Math.Min(pageSize, maxSize);
        return (p, size, (p - 1) * size);
    }
}
