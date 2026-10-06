namespace CursorPaginator.Core;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public class GenerateQueryAttribute : Attribute
{
    public string Table { get; set; }
    public string[]? Sortable { get; set; }
    public string[]? Exclude { get; set; }
    public string[]? Select { get; set; }
    public string Identifier { get; set; } = "Id";
    public NamingConvention? NamingConvention { get; set; }
    public DatabaseProvider? DatabaseProvider { get; set; }
    public Type Filter { get; set; }
    public Type Response { get; set; }
    public bool HasTenantFilter { get; set; } = false;

    /// <summary>
    /// Set when enum columns of this query are stored as their names (e.g. EF Core
    /// <c>HasConversion&lt;string&gt;()</c>) instead of integers. Enum filter values and cursor
    /// sort values are then bound as strings. Override per property with
    /// <see cref="QueryFieldAttribute.EnumAsString"/> or <see cref="QueryMapAttribute.EnumAsString"/>.
    /// </summary>
    public bool EnumsAsStrings { get; set; } = false;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class WhereAttribute : Attribute
{
    public required string Property { get; set; }
    public FilterOperator Operator { get; set; } = FilterOperator.Eq;
    public object? Value { get; set; }
    public string? CompareToProperty { get; set; }
    public string? Table { get; set; }
    public LogicalOperator LogicalOperator { get; set; } = LogicalOperator.And;
    public int Order { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class JoinAttribute : Attribute
{
    public required string Table { get; set; }
    public string? Alias { get; set; }
    public string[]? Select { get; set; }
    public JoinType Type { get; set; } = JoinType.Left;
    public int Order { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class JoinConditionAttribute : Attribute
{
    public required string JoinAlias { get; set; }
    public required string LeftTable { get; set; }
    public required string LeftColumn { get; set; }
    public FilterOperator Operator { get; set; } = FilterOperator.Eq;
    public required string RightTable { get; set; }
    public required string RightColumn { get; set; }
    public LogicalOperator LogicalOperator { get; set; } = LogicalOperator.And;
    public int Order { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class JoinWhereAttribute : Attribute
{
    public required string JoinAlias { get; set; }
    public required string Property { get; set; }
    public FilterOperator Operator { get; set; } = FilterOperator.Eq;
    public object? Value { get; set; }
    public string? CompareToProperty { get; set; }
    public LogicalOperator LogicalOperator { get; set; } = LogicalOperator.And;
    public int Order { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public class QueryFieldAttribute : Attribute
{
    public string? Table { get; set; }
    public string? ColumnName { get; set; }
    public FilterOperator[]? Operators { get; set; }
    public bool? Filterable { get; set; }
    public bool? Sortable { get; set; }
    public bool EnumAsString { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public class ComputedAttribute : Attribute
{
    public string Expression { get; set; }
    public bool? Filterable { get; set; }
    public bool? Sortable { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class QueryMapAttribute : Attribute
{
    public required string Property { get; set; }
    public string? Table { get; set; }
    public string? Alias { get; set; }
    public string? Column { get; set; }
    public bool? Filterable { get; set; }
    public bool? Sortable { get; set; }
    public FilterOperator[]? Operators { get; set; }
    public bool EnumAsString { get; set; }
    public int Order { get; set; }
}
