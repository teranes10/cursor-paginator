namespace CursorPaginator.Core;

public interface ICursorFilter { }

public interface ICursorQuery
{
    public int Limit { get; }
    public string? SortBy { get; }
    public bool SortDesc { get; }
}

public interface ICursorQuery<TFilter> : ICursorQuery where TFilter : ICursorFilter
{
    public TFilter? Filter { get; }
}

public readonly record struct CursorQueryResult<TResponse>
{
    public required string Sql { get; init; }

    public required Dictionary<string, object> Parameters { get; init; }

    public required byte[] FilterSortBytes { get; init; }

    public required byte SortFieldDir { get; init; }

    public required Func<TResponse, byte[], byte, string> EncodeCursor { get; init; }
}

public interface ICursorQueryHandler<TFilter, TResponse> where TFilter : ICursorFilter
{
    static abstract CursorQueryResult<TResponse> BuildQuery(string cursor, int limit);

    static abstract CursorQueryResult<TResponse> BuildQuery(TFilter? filter, string? sortBy, bool sortDesc, int limit);

    static abstract (string sql, Dictionary<string, object> parameters) BuildApproximateCountQuery();

    static abstract (string sql, Dictionary<string, object> parameters) BuildCountQuery(TFilter? filter);
}
