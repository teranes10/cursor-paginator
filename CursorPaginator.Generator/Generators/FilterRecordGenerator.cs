namespace CursorPaginator.Generator.Generators;

internal sealed class FilterRecordGenerator
{
    public static void GenerateFilterRecord(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config)
    {
        sb.AppendLine($"public record {entityInfo.BaseName}Filter : ICursorFilter");
        sb.AppendLine("{");

        foreach (var prop in entityInfo.FilterableProperties)
        {
            var filterType = prop.GetTypeFilter();
            sb.AppendLine($"    public {filterType} {prop.Name} {{ get; init; }}");
        }

        sb.AppendLine("}");
    }
}
