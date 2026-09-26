namespace CursorPaginator.Generator.Core;

internal sealed class JoinCondition
{
    public required string LeftTable { get; set; }
    public required string LeftColumn { get; set; }
    public required FilterOperator Operator { get; set; }
    public required string RightTable { get; set; }
    public required string RightColumn { get; set; }
    public LogicalOperator LogicalOperator { get; set; } = LogicalOperator.And;
    public int Order { get; set; }
}
