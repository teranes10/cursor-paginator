namespace CursorPaginator.Models;

public class PaginationRequest<TFilter>
{
    public int Limit { get; init; } = 10;
    public TFilter? Filter { get; init; }
    public string? SortBy { get; init; }
    public bool SortDesc { get; init; }
}
