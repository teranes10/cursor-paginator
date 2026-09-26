namespace CursorPaginator.Generator.Core;

internal sealed class PropertyInfo
{
    public string TableName { get; set; } = "";
    public string? TableAlias { get; set; }
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
    public string BaseType { get; init; } = "";
    public string ColumnName { get; init; } = "";
    public bool Filterable { get; init; }
    public bool Sortable { get; set; }
    public FilterOperator[] Operators { get; init; } = [];
    public QueryFieldType FieldType { get; init; }

    public string GetResolvedType()
    {
        if (TypeName == "string")
            return "required string";
        return TypeName;
    }

    public string GetFullColumnName() => string.IsNullOrEmpty(TableAlias)
            ? $"{TableName}.{ColumnName}"
            : $"{TableAlias}.{ColumnName}";

    public string GetTypeFilter() => FieldType switch
    {
        QueryFieldType.String => "StringFilter?",
        QueryFieldType.Number => $"NumberFilter<{BaseType}>?",
        QueryFieldType.DateTime => "DateTimeFilter?",
        QueryFieldType.DateOnly => "DateOnlyFilter?",
        QueryFieldType.Boolean => "BoolFilter?",
        QueryFieldType.Guid => "GuidFilter?",
        QueryFieldType.Enum => $"EnumFilter<{BaseType}>?",
        _ => throw new ArgumentException($"Unsupported query field type: {FieldType}")
    };

    public (string propertyName, string propertyType) GetFilterProperty(FilterOperator op) => op switch
    {
        FilterOperator.Eq => (Name, $"{BaseType}?"),
        FilterOperator.Ne => ($"{Name}NotEqual", $"{BaseType}?"),
        FilterOperator.Contains => ($"{Name}Contains", "string?"),
        FilterOperator.StartsWith => ($"{Name}StartsWith", "string?"),
        FilterOperator.EndsWith => ($"{Name}EndsWith", "string?"),
        FilterOperator.In => ($"{Name}In", $"{BaseType}[]?"),
        FilterOperator.NotIn => ($"{Name}NotIn", $"{BaseType}[]?"),
        FilterOperator.Gt => ($"{Name}GreaterThan", $"{BaseType}?"),
        FilterOperator.Gte => ($"{Name}{(FieldType == QueryFieldType.DateTime || FieldType == QueryFieldType.DateOnly ? "After" : "Min")}", $"{BaseType}?"),
        FilterOperator.Lt => ($"{Name}LessThan", $"{BaseType}?"),
        FilterOperator.Lte => ($"{Name}{(FieldType == QueryFieldType.DateTime || FieldType == QueryFieldType.DateOnly ? "Before" : "Max")}", $"{BaseType}?"),
        FilterOperator.IsNull => ($"{Name}IsNull", "bool?"),
        _ => throw new ArgumentException($"Unsupported filter operator: {op}")
    };

    public static (QueryFieldType fieldType, string baseType, string typeName) ResolvePropertyType(ITypeSymbol type)
    {
        var typeDisplay = type.ToDisplayString().TrimEnd('?');
        var underlyingType = type;

        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            underlyingType = named.TypeArguments[0];
            typeDisplay = underlyingType.ToDisplayString().TrimEnd('?');
        }

        var fullTypeName = underlyingType.ToDisplayString();
        var simpleTypeName = underlyingType.Name;

        if (simpleTypeName == "StringFilter" || fullTypeName.EndsWith(".StringFilter"))
        {
            return (QueryFieldType.String, "string", "string");
        }
        if (simpleTypeName == "DateTimeFilter" || fullTypeName.EndsWith(".DateTimeFilter"))
        {
            return (QueryFieldType.DateTime, "System.DateTime", "System.DateTime");
        }
        if (simpleTypeName == "DateOnlyFilter" || fullTypeName.EndsWith(".DateOnlyFilter"))
        {
            return (QueryFieldType.DateOnly, "System.DateOnly", "System.DateOnly");
        }
        if (simpleTypeName == "BoolFilter" || fullTypeName.EndsWith(".BoolFilter"))
        {
            return (QueryFieldType.Boolean, "bool", "bool");
        }
        if (simpleTypeName == "GuidFilter" || fullTypeName.EndsWith(".GuidFilter"))
        {
            return (QueryFieldType.Guid, "System.Guid", "System.Guid");
        }
        if ((simpleTypeName == "NumberFilter" || fullTypeName.Contains(".NumberFilter<")) && underlyingType is INamedTypeSymbol numGeneric && numGeneric.TypeArguments.Length == 1)
        {
            var innerType = numGeneric.TypeArguments[0].ToDisplayString();
            return (QueryFieldType.Number, innerType, innerType);
        }
        if ((simpleTypeName == "EnumFilter" || fullTypeName.Contains(".EnumFilter<")) && underlyingType is INamedTypeSymbol enumGeneric && enumGeneric.TypeArguments.Length == 1)
        {
            var innerType = enumGeneric.TypeArguments[0].ToDisplayString();
            return (QueryFieldType.Enum, innerType, innerType);
        }

        if (underlyingType.TypeKind == TypeKind.Enum)
            return (QueryFieldType.Enum, fullTypeName, fullTypeName);

        var fieldType = typeDisplay switch
        {
            "string" or "System.String" => QueryFieldType.String,
            "System.DateTime" => QueryFieldType.DateTime,
            "System.DateOnly" => QueryFieldType.DateOnly,
            "bool" or "System.Boolean" => QueryFieldType.Boolean,
            "System.Guid" or "Guid" => QueryFieldType.Guid,
            _ when IsNumericType(typeDisplay) => QueryFieldType.Number,
            _ => QueryFieldType.String
        };

        return (fieldType, typeDisplay, fullTypeName);
    }

    public static QueryFieldType InferFieldType(ITypeSymbol type) => ResolvePropertyType(type).fieldType;

    public static bool IsNumericType(string typeName) => typeName switch
    {
        "int" or "System.Int32" or
        "long" or "System.Int64" or
        "decimal" or "System.Decimal" or
        "double" or "System.Double" or
        "float" or "System.Single" or
        "short" or "System.Int16" or
        "byte" or "System.Byte" => true,
        _ => false
    };

    public override string ToString()
    {
        return $"PropertyInfo: Name: {Name}, ColumnName: {ColumnName}, TypeName: {TypeName}, BaseType: {BaseType}, Filterable: {Filterable}, Sortable: {Sortable}, Operators: {Operators}, FieldType: {FieldType}";
    }
}