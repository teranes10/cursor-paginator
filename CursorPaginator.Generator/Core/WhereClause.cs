namespace CursorPaginator.Generator.Core;

internal sealed class WhereClause
{
    public required string Property { get; set; }
    public required FilterOperator Operator { get; set; }
    public object? Value { get; set; }
    public string? CompareToProperty { get; set; }
    public string? Table { get; set; }
    public LogicalOperator LogicalOperator { get; set; } = LogicalOperator.And;
    public int Order { get; set; }
}
