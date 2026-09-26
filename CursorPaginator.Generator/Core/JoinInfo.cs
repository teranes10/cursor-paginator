namespace CursorPaginator.Generator.Core;

internal sealed class JoinInfo
{
    public string Table { get; set; } = "";
    public string Alias { get; set; } = "";
    public JoinType Type { get; set; }
    public List<JoinCondition> Conditions { get; set; } = [];
    public List<WhereClause> WhereClauses { get; set; } = [];
    public string[] SelectedColumns { get; set; } = [];
    public int Order { get; set; }
}
