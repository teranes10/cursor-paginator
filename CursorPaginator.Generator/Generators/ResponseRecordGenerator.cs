namespace CursorPaginator.Generator.Generators;

internal class ResponseRecordGenerator
{
    public static void GenerateResponseRecord(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config)
    {
        sb.AppendLine($"public record {entityInfo.BaseName}Response");
        sb.AppendLine("{");

        foreach (var prop in entityInfo.SelectProperties)
        {
            sb.AppendLine($"    public {prop.GetResolvedType()} {prop.Name} {{ get; init; }}");
        }

        sb.AppendLine("}");
    }
}
