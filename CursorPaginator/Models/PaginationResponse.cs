namespace CursorPaginator.Models;

public class PaginationResponse<T>
{
    public IEnumerable<T> Items { get; init; } = [];
    public string? NextCursor { get; init; }
    public bool HasNextPage { get; init; }
}