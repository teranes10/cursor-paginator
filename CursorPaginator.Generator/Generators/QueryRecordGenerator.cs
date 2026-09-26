internal sealed class QueryRecordGenerator
{
    public static void GenerateQueryRecord(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config)
    {
        sb.AppendLine($"public record {entityInfo.BaseName}Query : ICursorQuery<{entityInfo.BaseName}Filter>");
        sb.AppendLine("{");
        sb.AppendLine("    public int Limit { get; init; } = 10;");
        sb.AppendLine($"    public {entityInfo.BaseName}Filter? Filter {{ get; init; }}");
        sb.AppendLine("    public string? SortBy { get; init; }");
        sb.AppendLine("    public bool SortDesc { get; init; } = true;");
        sb.AppendLine("}");
    }
}