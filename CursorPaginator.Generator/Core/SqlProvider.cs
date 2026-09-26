namespace CursorPaginator.Generator.Core;

public sealed class SqlProviderConfig
{
    public DatabaseProvider DatabaseProvider { get; init; }
    public string ParameterPrefix { get; init; }
    public string InOperator { get; init; }
    public string NotInOperator { get; init; }
    public string LikeOperator { get; init; }
    public bool CaseSensitiveLike { get; init; }
    public string IdentifierQuote { get; init; }
    public bool UseArrayForIn { get; init; }

    public static SqlProviderConfig ForProvider(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.PostgreSQL => new()
        {
            DatabaseProvider = DatabaseProvider.PostgreSQL,
            ParameterPrefix = "@",
            InOperator = "= ANY",
            NotInOperator = "!= ALL",
            LikeOperator = "ILIKE",
            CaseSensitiveLike = false,
            IdentifierQuote = "\\\"",
            UseArrayForIn = true
        },
        DatabaseProvider.MySQL => new()
        {
            DatabaseProvider = DatabaseProvider.MySQL,
            ParameterPrefix = "@",
            InOperator = "IN",
            NotInOperator = "NOT IN",
            LikeOperator = "LIKE",
            CaseSensitiveLike = true,
            IdentifierQuote = "`",
            UseArrayForIn = false
        },
        _ => throw new ArgumentException($"Unsupported provider: {provider}")
    };
}