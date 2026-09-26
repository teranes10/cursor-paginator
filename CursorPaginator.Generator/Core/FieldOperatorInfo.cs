namespace CursorPaginator.Generator.Core;

internal class FieldOperatorInfo
{
    public required string PropertyName { get; set; }
    public required string BaseType { get; set; }
    public required QueryFieldType FieldType { get; set; }
    public required string ColumnName { get; set; }

    // Lowercase property name for parameter naming
    public string PropertyNameLower => char.ToLowerInvariant(PropertyName[0]) + PropertyName[1..];

    // Operator IDs (used for jump table indexing)
    public int IsNullTrueId { get; set; }
    public int IsNullFalseId { get; set; }
    public int EqId { get; set; }
    public int NeId { get; set; }
    public int GtId { get; set; } = -1;
    public int GteId { get; set; } = -1;
    public int LtId { get; set; } = -1;
    public int LteId { get; set; } = -1;
    public int StartsWithId { get; set; } = -1;
    public int EndsWithId { get; set; } = -1;
    public int ContainsId { get; set; } = -1;
    public int InId { get; set; }
    public int NotInId { get; set; }

    // Semantic parameter names for better debugging
    public string IsNullParam => $"@{PropertyNameLower}_isnull";
    public string IsNotNullParam => $"@{PropertyNameLower}_isnotnull";
    public string EqParam => $"@{PropertyNameLower}_eq";
    public string NeParam => $"@{PropertyNameLower}_ne";
    public string GtParam => $"@{PropertyNameLower}_gt";
    public string GteParam => $"@{PropertyNameLower}_gte";
    public string LtParam => $"@{PropertyNameLower}_lt";
    public string LteParam => $"@{PropertyNameLower}_lte";
    public string StartsWithParam => $"@{PropertyNameLower}_startswith";
    public string EndsWithParam => $"@{PropertyNameLower}_endswith";
    public string ContainsParam => $"@{PropertyNameLower}_contains";
    public string InParamPrefix => $"@{PropertyNameLower}_in";
    public string NotInParamPrefix => $"@{PropertyNameLower}_notin";
}
