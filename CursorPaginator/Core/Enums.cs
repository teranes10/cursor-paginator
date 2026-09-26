namespace CursorPaginator.Core;

public enum NamingConvention
{
    SnakeCase, PascalCase, CamelCase
}

public enum FilterOperator
{
    Eq, Ne, Contains, StartsWith, EndsWith,
    In, NotIn, Gt, Gte, Lt, Lte, IsNull
}

public enum LogicalOperator
{
    And, Or
}

public enum JoinType
{
    Inner, Left, Right, Full
}

public enum QueryFieldType
{
    String, Number, DateTime, DateOnly, Boolean, Guid, Enum
}

public enum DatabaseProvider
{
    PostgreSQL, MySQL
}