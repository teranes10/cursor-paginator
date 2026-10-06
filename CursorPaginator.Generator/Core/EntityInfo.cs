namespace CursorPaginator.Generator.Core;

internal sealed class EntityInfo
{
    public string TypeName { get; init; } = "";
    public string BaseName { get; init; } = "";
    public string Namespace { get; init; } = "";
    public string TableName { get; init; } = "";
    public string FilterTypeName { get; set; }
    public string FilterNamespace { get; set; }
    public string ResponseTypeName { get; set; }
    public string ResponseNamespace { get; set; }
    public NamingConvention NamingConvention { get; init; }
    public DatabaseProvider DatabaseProvider { get; init; }
    public PropertyInfo IdentifierProperty { get; init; }
    public List<PropertyInfo> Properties { get; init; } = [];
    public List<PropertyInfo> SelectProperties { get; init; } = [];
    public List<PropertyInfo> FilterableProperties { get; init; } = [];
    public List<PropertyInfo> SortableProperties { get; init; } = [];
    public List<ComputedFieldInfo> ComputedFields { get; init; } = [];
    public List<JoinInfo> Joins { get; init; } = [];
    public List<WhereClause> WhereClauses { get; init; } = [];
    public bool HasTenantFilter { get; init; }

    public static (EntityInfo? Entity, Diagnostic? Diagnostic) From(
        INamedTypeSymbol typeSymbol,
        DatabaseProvider defaultDbProvider = DatabaseProvider.MySQL,
        NamingConvention defaultNaming = NamingConvention.SnakeCase)
    {
        var generateQueryAttr = typeSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == "GenerateQueryAttribute");

        if (generateQueryAttr == null) return (null, null);

        try
        {
            var namingConvention = (NamingConvention)generateQueryAttr.GetNamedAttributeValue("NamingConvention", (int)defaultNaming);
            var databaseProvider = (DatabaseProvider)generateQueryAttr.GetNamedAttributeValue("DatabaseProvider", (int)defaultDbProvider);

            var tableName = generateQueryAttr.GetNamedAttributeValue("Table", "")?.ConvertNaming(namingConvention);
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return (null, Diagnostic.Create(
                    DiagnosticDescriptors.MissingTableName,
                    typeSymbol.Locations.FirstOrDefault(),
                    typeSymbol.Name));
            }

            var identifierField = generateQueryAttr.GetNamedAttributeValue("Identifier", GeneratorConstants.IdentifierField)!;
            var sortableFields = generateQueryAttr.GetNamedAttributeValue("Sortable", Array.Empty<string>())!;
            var excludeFields = generateQueryAttr.GetNamedAttributeValue("Exclude", Array.Empty<string>())!;
            var selectableFields = generateQueryAttr.GetNamedAttributeValue("Selectable", Array.Empty<string>())!;

            var enumsAsStrings = generateQueryAttr.GetNamedAttributeValue("EnumsAsStrings", false);

            var queryMapAttrs = typeSymbol.GetAttributes()
                .Where(attr => attr.AttributeClass?.Name == "QueryMapAttribute")
                .ToList();

            var filterSymbol = generateQueryAttr.GetNamedAttributeValue<INamedTypeSymbol>("Filter", null);
            if (filterSymbol == null)
            {
                return (null, Diagnostic.Create(
                   DiagnosticDescriptors.MissingFilterType,
                   typeSymbol.Locations.FirstOrDefault(),
                   typeSymbol.Name));
            }

            static IEnumerable<IPropertySymbol> GetAllProperties(ITypeSymbol? type)
            {
                var current = type;
                while (current != null && current.SpecialType != SpecialType.System_Object)
                {
                    foreach (var member in current.GetMembers().OfType<IPropertySymbol>())
                    {
                        yield return member;
                    }
                    current = current.BaseType;
                }
            }

            var filterMembers = GetAllProperties(filterSymbol)
                .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsImplicitlyDeclared && p.Name != "EqualityContract")
                .GroupBy(p => p.Name)
                .Select(g => g.First())
                .ToList();
            var filterableFields = filterMembers.Select(x => x.Name).ToHashSet();

            var responseSymbol = generateQueryAttr.GetNamedAttributeValue<INamedTypeSymbol>("Response", null);
            if (responseSymbol == null)
            {
                return (null, Diagnostic.Create(
                   DiagnosticDescriptors.MissingResponseType,
                   typeSymbol.Locations.FirstOrDefault(),
                   typeSymbol.Name));
            }

            var responseMembers = GetAllProperties(responseSymbol)
                .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsImplicitlyDeclared && p.Name != "EqualityContract")
                .Where(p => p.SetMethod != null || p.GetAttributes().Any(attr => attr.AttributeClass?.Name == "ComputedAttribute"))
                .GroupBy(p => p.Name)
                .Select(g => g.First())
                .ToList();

            var responsePropertyNames = responseMembers.Select(p => p.Name).ToHashSet();

            var properties = new List<PropertyInfo>();
            foreach (var propSymbol in responseMembers)
            {
                // [Computed] properties are handled entirely by the
                // ComputedFields pass below, which emits them as a SQL
                // expression (e.g. `(ended_at IS NULL) AS is_ongoing`), not
                // a literal column. Without this guard they'd ALSO end up
                // here with an auto-derived column name that doesn't exist
                // in the table, since `SetMethod != null` is true for
                // `init`-only properties regardless of [Computed].
                if (propSymbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "ComputedAttribute"))
                    continue;

                var queryMapAttr = queryMapAttrs.FirstOrDefault(attr => attr.GetNamedAttributeValue<string>("Property") == propSymbol.Name);
                var queryFieldAttr = propSymbol.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.Name == "QueryFieldAttribute");

                var filterPropSymbol = filterMembers.FirstOrDefault(m => m.Name == propSymbol.Name);
                var filterQueryFieldAttr = filterPropSymbol?.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.Name == "QueryFieldAttribute");

                var table = queryMapAttr?.GetNamedAttributeValue<string>("Alias")
                    ?? queryMapAttr?.GetNamedAttributeValue<string>("Table")
                    ?? queryFieldAttr?.GetNamedAttributeValue<string>("Table") 
                    ?? filterQueryFieldAttr?.GetNamedAttributeValue<string>("Table") 
                    ?? tableName;

                var columnName = queryMapAttr?.GetNamedAttributeValue<string>("Column")
                    ?? queryFieldAttr?.GetNamedAttributeValue<string>("ColumnName") 
                    ?? filterQueryFieldAttr?.GetNamedAttributeValue<string>("ColumnName") 
                    ?? propSymbol.Name.ConvertNaming(namingConvention);

                var isFilterable = queryMapAttr?.GetNamedAttributeValue<bool?>("Filterable")
                    ?? queryFieldAttr?.GetNamedAttributeValue<bool?>("Filterable") 
                    ?? filterQueryFieldAttr?.GetNamedAttributeValue<bool?>("Filterable") 
                    ?? filterableFields.Contains(propSymbol.Name);

                var isSortable = queryMapAttr?.GetNamedAttributeValue<bool?>("Sortable")
                    ?? queryFieldAttr?.GetNamedAttributeValue<bool?>("Sortable") 
                    ?? (sortableFields.Contains(propSymbol.Name) || propSymbol.Name == identifierField);

                var operators = queryMapAttr?.GetNamedAttributeValue("Operators", Array.Empty<int>())?.Select(o => (FilterOperator)o).ToArray()
                    ?? queryFieldAttr?.GetNamedAttributeValue("Operators", Array.Empty<int>())?.Select(o => (FilterOperator)o).ToArray() 
                    ?? filterQueryFieldAttr?.GetNamedAttributeValue("Operators", Array.Empty<int>())?.Select(o => (FilterOperator)o).ToArray() 
                    ?? [];

                var (fieldType, baseType, _) = PropertyInfo.ResolvePropertyType(propSymbol.Type);

                var enumAsString = fieldType == QueryFieldType.Enum
                    && (queryMapAttr?.GetNamedAttributeValue<bool?>("EnumAsString")
                        ?? queryFieldAttr?.GetNamedAttributeValue<bool?>("EnumAsString")
                        ?? filterQueryFieldAttr?.GetNamedAttributeValue<bool?>("EnumAsString")
                        ?? enumsAsStrings);

                properties.Add(new PropertyInfo
                {
                    Name = propSymbol.Name,
                    TypeName = propSymbol.Type.ToDisplayString(),
                    BaseType = baseType,
                    TableName = table,
                    ColumnName = columnName,
                    Filterable = isFilterable,
                    Sortable = isSortable,
                    Operators = operators,
                    FieldType = fieldType,
                    EnumAsString = enumAsString
                });
            }

            // Handle filter-only properties that exist on Filter class but not on Response class
            foreach (var filterPropSymbol in filterMembers)
            {
                if (responsePropertyNames.Contains(filterPropSymbol.Name))
                    continue;

                var queryMapAttr = queryMapAttrs.FirstOrDefault(attr => attr.GetNamedAttributeValue<string>("Property") == filterPropSymbol.Name);
                var queryFieldAttr = filterPropSymbol.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.Name == "QueryFieldAttribute");

                var table = queryMapAttr?.GetNamedAttributeValue<string>("Alias")
                    ?? queryMapAttr?.GetNamedAttributeValue<string>("Table")
                    ?? queryFieldAttr?.GetNamedAttributeValue<string>("Table") 
                    ?? tableName;

                var columnName = queryMapAttr?.GetNamedAttributeValue<string>("Column")
                    ?? queryFieldAttr?.GetNamedAttributeValue<string>("ColumnName") 
                    ?? filterPropSymbol.Name.ConvertNaming(namingConvention);

                var isFilterable = queryMapAttr?.GetNamedAttributeValue<bool?>("Filterable")
                    ?? queryFieldAttr?.GetNamedAttributeValue<bool?>("Filterable", true) 
                    ?? true;

                var isSortable = queryMapAttr?.GetNamedAttributeValue<bool?>("Sortable")
                    ?? queryFieldAttr?.GetNamedAttributeValue<bool?>("Sortable", false)
                    ?? sortableFields.Contains(filterPropSymbol.Name);

                var operators = queryMapAttr?.GetNamedAttributeValue("Operators", Array.Empty<int>())?.Select(o => (FilterOperator)o).ToArray()
                    ?? queryFieldAttr?.GetNamedAttributeValue("Operators", Array.Empty<int>())?.Select(o => (FilterOperator)o).ToArray() 
                    ?? [];

                var (fieldType, baseType, typeNameStr) = PropertyInfo.ResolvePropertyType(filterPropSymbol.Type);

                var enumAsString = fieldType == QueryFieldType.Enum
                    && (queryMapAttr?.GetNamedAttributeValue<bool?>("EnumAsString")
                        ?? queryFieldAttr?.GetNamedAttributeValue<bool?>("EnumAsString")
                        ?? enumsAsStrings);

                properties.Add(new PropertyInfo
                {
                    Name = filterPropSymbol.Name,
                    TypeName = typeNameStr,
                    BaseType = baseType,
                    TableName = table,
                    ColumnName = columnName,
                    Filterable = isFilterable,
                    Sortable = isSortable,
                    Operators = operators,
                    FieldType = fieldType,
                    EnumAsString = enumAsString
                });
            }

            var computedFields = new List<ComputedFieldInfo>();
            foreach (var propSymbol in responseMembers)
            {
                var (computed, diag) = ComputedFieldInfo.From(propSymbol);
                if (diag != null) return (null, diag);
                if (computed != null)
                {
                    computedFields.Add(computed);
                }
            }

            var whereClauses = ParseWhereClauses(typeSymbol, namingConvention);

            var joins = ParseJoins(typeSymbol, namingConvention);

            var hasTenantFilter = generateQueryAttr.GetNamedAttributeValue("HasTenantFilter", false);

            var selectProperties = properties.Where(p => responsePropertyNames.Contains(p.Name) && !excludeFields.Contains(p.Name)).ToList();

            var identifierProp = properties.FirstOrDefault(x => x.Name == identifierField) ?? throw new ArgumentException("Identifier field not found.");
            identifierProp.Sortable = true;

            var sortableProperties = properties.Where(p => p.Sortable).ToList();
            if (!sortableProperties.Any(p => p.Name == identifierField))
            {
                sortableProperties.Insert(0, identifierProp);
            }

            return (new EntityInfo
            {
                TypeName = typeSymbol.Name,
                BaseName = typeSymbol.Name.EndsWith("Spec") ? typeSymbol.Name.Substring(0, typeSymbol.Name.Length - 4) : typeSymbol.Name,
                Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
                TableName = tableName,
                FilterTypeName = filterSymbol.Name,
                FilterNamespace = filterSymbol.ContainingNamespace.ToDisplayString(),
                ResponseTypeName = responseSymbol.Name,
                ResponseNamespace = responseSymbol.ContainingNamespace.ToDisplayString(),
                NamingConvention = namingConvention,
                DatabaseProvider = databaseProvider,
                Properties = selectableFields.Length != 0 ? [.. properties.Where(p => selectableFields.Contains(p.Name))] : properties,
                FilterableProperties = [.. properties.Where(p => p.Filterable)],
                SortableProperties = sortableProperties,
                SelectProperties = selectProperties,
                ComputedFields = computedFields,
                IdentifierProperty = identifierProp,
                Joins = joins,
                WhereClauses = whereClauses,
                HasTenantFilter = hasTenantFilter
            }, null);
        }
        catch (Exception ex)
        {
            return (null, Diagnostic.Create(
                DiagnosticDescriptors.GenerationError,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name,
                ex.Message));
        }
    }

    private static List<WhereClause> ParseWhereClauses(INamedTypeSymbol typeSymbol, NamingConvention namingConvention)
    {
        var whereAttrs = typeSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name == "WhereAttribute")
            .ToList();

        var clauses = new List<WhereClause>();

        for (int i = 0; i < whereAttrs.Count; i++)
        {
            var attr = whereAttrs[i];
            clauses.Add(new WhereClause
            {
                Property = attr.GetNamedAttributeValue<string>("Property")!.ConvertNaming(namingConvention),
                Operator = (FilterOperator)attr.GetNamedAttributeValue("Operator", (int)FilterOperator.Eq),
                Value = attr.GetNamedAttributeValue<object>("Value"),
                CompareToProperty = attr.GetNamedAttributeValue<string>("CompareToProperty")?.ConvertNaming(namingConvention),
                Table = attr.GetNamedAttributeValue<string>("Table"),
                LogicalOperator = (LogicalOperator)attr.GetNamedAttributeValue("LogicalOperator", (int)LogicalOperator.And),
                Order = attr.GetNamedAttributeValue("Order", i)
            });
        }

        return clauses.OrderBy(c => c.Order).ToList();
    }

    private static List<JoinInfo> ParseJoins(INamedTypeSymbol typeSymbol, NamingConvention namingConvention)
    {
        var joinAttrs = typeSymbol.GetAttributes().Where(attr => attr.AttributeClass?.Name == "JoinAttribute").ToList();

        var joins = new List<JoinInfo>();

        for (int i = 0; i < joinAttrs.Count; i++)
        {
            var joinAttr = joinAttrs[i];
            var table = joinAttr.GetNamedAttributeValue<string>("Table")!;
            var alias = joinAttr.GetNamedAttributeValue<string>("Alias") ?? table;
            var selectedColumns = joinAttr.GetNamedAttributeValue<string[]>("Select") ?? [];
            var joinType = (JoinType)joinAttr.GetNamedAttributeValue("Type", (int)JoinType.Left);
            var order = joinAttr.GetNamedAttributeValue("Order", i);

            var conditionAttrs = typeSymbol.GetAttributes()
                .Where(attr => attr.AttributeClass?.Name == "JoinConditionAttribute" && attr.GetNamedAttributeValue<string>("JoinAlias") == alias)
                .ToList();

            var conditions = new List<JoinCondition>();
            for (int cIdx = 0; cIdx < conditionAttrs.Count; cIdx++)
            {
                var attr = conditionAttrs[cIdx];
                conditions.Add(new JoinCondition
                {
                    LeftTable = attr.GetNamedAttributeValue<string>("LeftTable")!,
                    LeftColumn = attr.GetNamedAttributeValue<string>("LeftColumn")!.ConvertNaming(namingConvention),
                    Operator = (FilterOperator)attr.GetNamedAttributeValue("Operator", (int)FilterOperator.Eq),
                    RightTable = attr.GetNamedAttributeValue<string>("RightTable")!,
                    RightColumn = attr.GetNamedAttributeValue<string>("RightColumn")!.ConvertNaming(namingConvention),
                    LogicalOperator = (LogicalOperator)attr.GetNamedAttributeValue("LogicalOperator", (int)LogicalOperator.And),
                    Order = attr.GetNamedAttributeValue("Order", cIdx)
                });
            }

            var whereAttrs = typeSymbol.GetAttributes()
                .Where(attr => attr.AttributeClass?.Name == "JoinWhereAttribute" && attr.GetNamedAttributeValue<string>("JoinAlias") == alias)
                .ToList();

            var joinWheres = new List<WhereClause>();
            for (int wIdx = 0; wIdx < whereAttrs.Count; wIdx++)
            {
                var attr = whereAttrs[wIdx];
                joinWheres.Add(new WhereClause
                {
                    Property = attr.GetNamedAttributeValue<string>("Property")!.ConvertNaming(namingConvention),
                    Operator = (FilterOperator)attr.GetNamedAttributeValue("Operator", (int)FilterOperator.Eq),
                    Value = attr.GetNamedAttributeValue<object>("Value"),
                    CompareToProperty = attr.GetNamedAttributeValue<string>("CompareToProperty")?.ConvertNaming(namingConvention),
                    LogicalOperator = (LogicalOperator)attr.GetNamedAttributeValue("LogicalOperator", (int)LogicalOperator.And),
                    Order = attr.GetNamedAttributeValue("Order", wIdx)
                });
            }

            joins.Add(new JoinInfo
            {
                Table = table.ConvertNaming(namingConvention),
                Alias = alias,
                Type = joinType,
                Conditions = conditions.OrderBy(c => c.Order).ToList(),
                WhereClauses = joinWheres.OrderBy(w => w.Order).ToList(),
                SelectedColumns = selectedColumns,
                Order = order
            });
        }

        return [.. joins.OrderBy(j => j.Order)];
    }
}