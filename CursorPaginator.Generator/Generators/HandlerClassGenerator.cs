
internal class HandlerClassGenerator
{
    public static void GenerateHandlerClass(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config)
    {
        sb.AppendLine($"partial class {entityInfo.TypeName} : ICursorQueryHandler<{entityInfo.FilterTypeName}, {entityInfo.ResponseTypeName}>");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly string BaseQuery = @\"");
        sb.AppendLine(SqlQueryBuilder.BuildBaseQuery(entityInfo, config));
        sb.AppendLine("\";");
        string approxQuery;
        if (entityInfo.TableName.Contains('.'))
        {
            var parts = entityInfo.TableName.Split('.');
            approxQuery = $"SELECT table_rows FROM information_schema.tables WHERE table_schema = '{parts[0]}' AND table_name = '{parts[1]}'";
        }
        else
        {
            approxQuery = $"SELECT table_rows FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '{entityInfo.TableName}'";
        }
        sb.AppendLine($"    private const string ApproximateCountQuery = \"{approxQuery}\";");
        sb.Append("    private static readonly bool hasBaseWhereClause = ").Append(entityInfo.WhereClauses.Count > 0 || entityInfo.HasTenantFilter ? "true" : "false").Append(';');
        sb.AppendLine();

        var parameterMap = ParameterMap.CalculateParameterMap(entityInfo);
        var runtimeParamStart = parameterMap.RuntimeParamStart;
        var limitParamNum = runtimeParamStart + 2;
        var typeHashCode = $"{Helpers.StableHashSha256(entityInfo.TypeName)}";

        sb.AppendLine($"    private const int TypeHashCode = {typeHashCode};");
        sb.AppendLine();

        GenerateBuildQuery(sb, entityInfo, config, parameterMap, runtimeParamStart, limitParamNum);
        sb.AppendLine();
        GenerateBuildQuery2(sb, entityInfo, config, parameterMap, runtimeParamStart, limitParamNum);
        sb.AppendLine();
        GenerateBuildApproximateCountQuery(sb, entityInfo, config, parameterMap, runtimeParamStart);
        sb.AppendLine();
        GenerateJumpTables(sb, entityInfo, config, parameterMap, runtimeParamStart);
        sb.AppendLine();
        GenerateHelperMethods(sb, entityInfo, config, runtimeParamStart);
        sb.AppendLine();
        GenerateEncodeCursor(sb, entityInfo, config, runtimeParamStart);
        sb.AppendLine();
        GenerateDecodeLastModified(sb, entityInfo, config);
        sb.AppendLine("}");
    }

    private static void GenerateBuildQuery(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config,
        ParameterMap paramMap, int runtimeParamStart, int limitParamNum)
    {
        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveOptimization)]");
        sb.AppendLine($"    public static CursorQueryResult<{entityInfo.ResponseTypeName}> BuildQuery(string cursor, int limit)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (string.IsNullOrWhiteSpace(cursor))");
        sb.AppendLine("            throw new ArgumentException(\"Cursor value cannot be empty.\");");
        sb.AppendLine("        if (limit <= 0)");
        sb.AppendLine("            throw new ArgumentException(\"Limit must be greater than zero\");");
        sb.AppendLine();
        sb.AppendLine("        var parameters = new Dictionary<string, object>(32);");
        sb.AppendLine();
        sb.AppendLine("        var sb = new StringBuilder(2048);");
        sb.AppendLine("        sb.Append(BaseQuery);");
        sb.AppendLine();
        sb.AppendLine("        if (!CursorSecurity.VerifyAndExtract(cursor, out var cursorData))");
        sb.AppendLine("            throw new ArgumentException(\"Invalid cursor\");");
        sb.AppendLine();
        sb.AppendLine("        using var ms = new MemoryStream(cursorData);");
        sb.AppendLine("        using var reader = new BinaryReader(ms);");
        sb.AppendLine();
        sb.AppendLine("        if (reader.ReadInt32() != TypeHashCode)");
        sb.AppendLine("            throw new ArgumentException(\"Cursor type mismatch\");");
        sb.AppendLine();
        sb.AppendLine("        var filterCount = reader.ReadByte();");
        sb.AppendLine("        if (filterCount > 0)");
        sb.AppendLine("        {");

        sb.AppendLine("            if (hasBaseWhereClause) sb.Append(\" AND \");");
        sb.AppendLine("            else sb.Append(\" WHERE \");");

        sb.AppendLine("            AppendFilterConditions(sb, reader, parameters, filterCount);");
        sb.AppendLine("        }");

        sb.AppendLine();
        sb.AppendLine("        var sortFieldDir = reader.ReadByte();");
        sb.AppendLine();
        sb.AppendLine("        if (sortFieldDir >= CursorJumpTable.Length)");
        sb.AppendLine("            throw new ArgumentException($\"Invalid sortFieldDir: {sortFieldDir}\");");
        sb.AppendLine();
        sb.AppendLine("        long lengthSoFar = ms.Position; ms.Position = 0;");
        sb.AppendLine("        var filterSortBytes = reader.ReadBytes((int)lengthSoFar); ");
        sb.AppendLine();
        sb.AppendLine("        var cursorFieldCount = reader.ReadByte();");
        sb.AppendLine("        if (cursorFieldCount > 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (hasBaseWhereClause || filterCount > 0) sb.Append(\" AND \");");
        sb.AppendLine("            else sb.Append(\" WHERE \");");
        sb.AppendLine();
        sb.AppendLine("            CursorJumpTable[sortFieldDir](sb, reader, parameters);");
        sb.AppendLine();
        sb.AppendLine("            AppendOrderBy(sb, sortFieldDir);");
        sb.AppendLine("        }");

        sb.AppendLine();
        sb.AppendLine($"        sb.Append(\" LIMIT {config.ParameterPrefix}limit\");");
        sb.AppendLine($"        parameters[\"{config.ParameterPrefix}limit\"] = limit;");
        sb.AppendLine();
        sb.AppendLine($"        return new CursorQueryResult<{entityInfo.ResponseTypeName}>");
        sb.AppendLine("        {");
        sb.AppendLine("            Sql = sb.ToString(),");
        sb.AppendLine("            Parameters = parameters,");
        sb.AppendLine("            FilterSortBytes = filterSortBytes,");
        sb.AppendLine("            SortFieldDir = sortFieldDir,");
        sb.AppendLine("            EncodeCursor = EncodeCursor");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
    }

    private static void GenerateBuildQuery2(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config,
       ParameterMap paramMap, int runtimeParamStart, int limitParamNum)
    {
        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveOptimization)]");
        sb.AppendLine($"    public static CursorQueryResult<{entityInfo.ResponseTypeName}> BuildQuery({entityInfo.FilterTypeName}? filter = null, string? sortBy = null, bool sortDesc = false, int limit = 10)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (limit <= 0)");
        sb.AppendLine("            throw new ArgumentException(\"Limit must be greater than zero\");");
        sb.AppendLine();
        sb.AppendLine("        var parameters = new Dictionary<string, object>(32);");
        sb.AppendLine();
        sb.AppendLine("        var sb = new StringBuilder(2048);");
        sb.AppendLine("        sb.Append(BaseQuery);");
        sb.AppendLine();

        GeneratePrepareFilterSortBytes(sb, entityInfo, config, paramMap);

        sb.AppendLine();
        sb.AppendLine("        using var ms = new MemoryStream(filterSortBytes, false);");
        sb.AppendLine("        using var reader = new BinaryReader(ms);");
        sb.AppendLine();
        sb.AppendLine("        reader.ReadInt32(); // Skip type hash");
        sb.AppendLine();
        sb.AppendLine("        var filterCount = reader.ReadByte();");
        sb.AppendLine("        if (filterCount > 0)");
        sb.AppendLine("        {");

        sb.AppendLine("            if (hasBaseWhereClause) sb.Append(\" AND \");");
        sb.AppendLine("            else sb.Append(\" WHERE \");");

        sb.AppendLine("            AppendFilterConditions(sb, reader, parameters, filterCount);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        AppendOrderBy(sb, sortFieldDir);");

        sb.AppendLine();
        sb.AppendLine($"        sb.Append(\" LIMIT {config.ParameterPrefix}limit\");");
        sb.AppendLine($"        parameters[\"{config.ParameterPrefix}limit\"] = limit;");
        sb.AppendLine();
        sb.AppendLine($"        return new CursorQueryResult<{entityInfo.ResponseTypeName}>");
        sb.AppendLine("        {");
        sb.AppendLine("            Sql = sb.ToString(),");
        sb.AppendLine("            Parameters = parameters,");
        sb.AppendLine("            FilterSortBytes = filterSortBytes,");
        sb.AppendLine("            SortFieldDir = sortFieldDir,");
        sb.AppendLine("            EncodeCursor = EncodeCursor");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
    }


    private static void GenerateBuildApproximateCountQuery(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config,
        ParameterMap paramMap, int runtimeParamStart)
    {
        // Approximate count (fast, no filters - uses table statistics)
        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"    public static (string sql, Dictionary<string, object> parameters) BuildApproximateCountQuery()");
        sb.AppendLine("    {");
        sb.AppendLine("        return (ApproximateCountQuery, new Dictionary<string, object>());");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Accurate count with filter support
        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveOptimization)]");
        sb.AppendLine($"    public static (string sql, Dictionary<string, object> parameters) BuildCountQuery({entityInfo.FilterTypeName}? filter)");
        sb.AppendLine("    {");
        sb.AppendLine("        var parameters = new Dictionary<string, object>(32);");
        sb.AppendLine();
        var fromAndJoins = SqlQueryBuilder.BuildFromAndJoins(entityInfo, config).Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
        sb.AppendLine($"        var sb = new StringBuilder(\"SELECT COUNT(*) {fromAndJoins}\");");
        sb.AppendLine();

        // Add base WHERE clauses if any
        if (entityInfo.HasTenantFilter || entityInfo.WhereClauses.Count > 0)
        {
            sb.AppendLine("        sb.Append(\" WHERE \");");
            bool hasTenant = false;
            if (entityInfo.HasTenantFilter)
            {
                sb.AppendLine($"        sb.Append(\"{Helpers.QuoteIdentifier(entityInfo.TableName, config.IdentifierQuote)}.{Helpers.QuoteIdentifier("tenant_id", config.IdentifierQuote)} = @TenantId\");");
                hasTenant = true;
            }

            if (entityInfo.WhereClauses.Count > 0)
            {
                if (hasTenant)
                {
                    sb.AppendLine("        sb.Append(\" AND \");");
                }
                var whereStr = SqlQueryBuilder.BuildWhereClause(entityInfo.WhereClauses, entityInfo.NamingConvention, config, entityInfo.TableName);
                sb.AppendLine($"        sb.Append(\"{whereStr}\");");
            }
        }

        sb.AppendLine("        if (filter == null)");
        sb.AppendLine("            return (sb.ToString(), parameters);");
        sb.AppendLine();

        // Build filter using same logic as BuildQuery2
        sb.AppendLine("        using var writerMs = new MemoryStream(256);");
        sb.AppendLine("        using var writer = new BinaryWriter(writerMs);");
        sb.AppendLine();
        sb.AppendLine("        writer.Write(TypeHashCode);");
        sb.AppendLine();

        GenerateFilterListBuilding(sb, entityInfo, config, paramMap);

        sb.AppendLine();
        sb.AppendLine("        using var ms = new MemoryStream(writerMs.ToArray(), false);");
        sb.AppendLine("        using var reader = new BinaryReader(ms);");
        sb.AppendLine();
        sb.AppendLine("        reader.ReadInt32(); // Skip type hash");
        sb.AppendLine();
        sb.AppendLine("        var filterCount = reader.ReadByte();");
        sb.AppendLine("        if (filterCount > 0)");
        sb.AppendLine("        {");

        sb.AppendLine("            if (hasBaseWhereClause) sb.Append(\" AND \");");
        sb.AppendLine("            else sb.Append(\" WHERE \");");

        sb.AppendLine("            AppendFilterConditions(sb, reader, parameters, filterCount);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return (sb.ToString(), parameters);");
        sb.AppendLine("    }");
    }

    private static void GenerateFilterListBuilding(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config, ParameterMap paramMap)
    {
        var idProp = entityInfo.SortableProperties.First(p => p.Name == entityInfo.IdentifierProperty.Name);

        sb.AppendLine("        if(filter != null) {");
        sb.AppendLine("            var filterList = new List<(byte fieldOpId, object value)>(16);");
        sb.AppendLine();

        foreach (var prop in entityInfo.FilterableProperties)
        {
            var propName = prop.Name;
            var fieldIdx = entityInfo.FilterableProperties.IndexOf(prop);
            var fieldInfo = paramMap.FieldOperators[fieldIdx];

            sb.AppendLine($"            if (filter?.{propName} != null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                var f = filter.{propName}.Value;");

            sb.AppendLine($"                if (f.IsNull.HasValue) filterList.Add((f.IsNull.Value ? (byte){fieldInfo.IsNullTrueId} : (byte){fieldInfo.IsNullFalseId}, f.IsNull.Value));");

            var isString = prop.FieldType == QueryFieldType.String;
            var nullCheck = isString ? "!string.IsNullOrWhiteSpace(f.Eq)" : "f.Eq.HasValue";
            var valueAccess = isString ? "f.Eq!" : "f.Eq.Value";

            sb.AppendLine($"                if ({nullCheck}) filterList.Add(({fieldInfo.EqId}, (object){valueAccess}));");
            sb.AppendLine($"                if ({nullCheck.Replace("Eq", "Ne")}) filterList.Add(({fieldInfo.NeId}, (object){valueAccess.Replace("Eq", "Ne")}));");

            if (prop.FieldType == QueryFieldType.Number || prop.FieldType == QueryFieldType.DateTime || prop.FieldType == QueryFieldType.DateOnly)
            {
                sb.AppendLine($"                if (f.Gt.HasValue) filterList.Add(((byte){fieldInfo.GtId}, (object)f.Gt.Value));");
                sb.AppendLine($"                if (f.Gte.HasValue) filterList.Add(((byte){fieldInfo.GteId}, (object)f.Gte.Value));");
                sb.AppendLine($"                if (f.Lt.HasValue) filterList.Add(((byte){fieldInfo.LtId}, (object)f.Lt.Value));");
                sb.AppendLine($"                if (f.Lte.HasValue) filterList.Add(((byte){fieldInfo.LteId}, (object)f.Lte.Value));");
            }

            if (prop.FieldType == QueryFieldType.String)
            {
                sb.AppendLine($"                if (!string.IsNullOrWhiteSpace(f.StartsWith)) filterList.Add(((byte){fieldInfo.StartsWithId}, (object)f.StartsWith!));");
                sb.AppendLine($"                if (!string.IsNullOrWhiteSpace(f.EndsWith)) filterList.Add(((byte){fieldInfo.EndsWithId}, (object)f.EndsWith!));");
                sb.AppendLine($"                if (!string.IsNullOrWhiteSpace(f.Contains)) filterList.Add(((byte){fieldInfo.ContainsId}, (object)f.Contains!));");
            }

            if (prop.FieldType != QueryFieldType.Boolean)
            {
                sb.AppendLine($"                if (f.In != null && f.In.Length > 0) filterList.Add(((byte){fieldInfo.InId}, (object)f.In));");
                sb.AppendLine($"                if (f.NotIn != null && f.NotIn.Length > 0) filterList.Add(((byte){fieldInfo.NotInId}, (object)f.NotIn));");
            }

            sb.AppendLine("            }");
            sb.AppendLine();
        }

        sb.AppendLine("            writer.Write((byte)filterList.Count);");
        sb.AppendLine("                ");
        sb.AppendLine("            foreach (var (fieldOpId, value) in CollectionsMarshal.AsSpan(filterList))");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.Write(fieldOpId);");
        sb.AppendLine("                if (fieldOpId >= FilterWriteJumpTable.Length)");
        sb.AppendLine("                    throw new ArgumentOutOfRangeException(nameof(fieldOpId), $\"Invalid fieldOpId: {fieldOpId}\");");
        sb.AppendLine("                FilterWriteJumpTable[fieldOpId](writer, value);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        else {");
        sb.AppendLine("            writer.Write((byte)0);");
        sb.AppendLine("        }");
    }


    private static void GenerateJumpTables(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config,
        ParameterMap paramMap, int runtimeParamStart)
    {
        // Generate filter read jump table
        sb.AppendLine("    private delegate void FilterReadHandler(StringBuilder sql, BinaryReader reader, Dictionary<string, object> p);");
        sb.AppendLine("    ");
        sb.AppendLine("    private static readonly FilterReadHandler[] FilterReadJumpTable = ");
        sb.AppendLine("    {");

        var dynamicParamIndex = runtimeParamStart + 3;

        foreach (var field in paramMap.FieldOperators)
        {
            var col = Helpers.QuoteIdentifier(field.ColumnName, config.IdentifierQuote);
            var readMethod = Helpers.GetBinaryReadMethod(field.BaseType);
            var fieldNameLower = field.PropertyName.Substring(0, 1).ToLower() + field.PropertyName.Substring(1);

            sb.AppendLine("        (sql, r, p) => { sql.Append($\"{col} IS NULL\"); r.ReadBoolean(); },".Replace("{col}", col));
            sb.AppendLine("        (sql, r, p) => { sql.Append($\"{col} IS NOT NULL\"); r.ReadBoolean(); },".Replace("{col}", col));
            sb.AppendLine($"        (sql, r, p) => {{ sql.Append(\"{col} = {field.EqParam}\"); p[\"{field.EqParam}\"] = {readMethod}; }},".Replace("{col}", col));
            sb.AppendLine($"        (sql, r, p) => {{ sql.Append(\"{col} != {field.NeParam}\"); p[\"{field.NeParam}\"] = {readMethod}; }},".Replace("{col}", col));

            if (field.GtId >= 0)
            {
                sb.AppendLine($"        (sql, r, p) => {{ sql.Append(\"{col} > {field.GtParam}\"); p[\"{field.GtParam}\"] = {readMethod}; }},".Replace("{col}", col));
                sb.AppendLine($"        (sql, r, p) => {{ sql.Append(\"{col} >= {field.GteParam}\"); p[\"{field.GteParam}\"] = {readMethod}; }},".Replace("{col}", col));
                sb.AppendLine($"        (sql, r, p) => {{ sql.Append(\"{col} < {field.LtParam}\"); p[\"{field.LtParam}\"] = {readMethod}; }},".Replace("{col}", col));
                sb.AppendLine($"        (sql, r, p) => {{ sql.Append(\"{col} <= {field.LteParam}\"); p[\"{field.LteParam}\"] = {readMethod}; }},".Replace("{col}", col));
            }

            if (field.StartsWithId >= 0)
            {
                sb.Append($"        (sql, r, p) => {{ ");
                sb.Append($" sql.Append(\"{col} {config.LikeOperator} {field.StartsWithParam}\");".Replace("{col}", col));
                sb.Append($" p[\"{field.StartsWithParam}\"] = FilterHelpers.EscapeLike({readMethod}) + \"%\";");
                sb.AppendLine(" },");

                sb.Append($"        (sql, r, p) => {{ ");
                sb.Append($" sql.Append(\"{col} {config.LikeOperator} {field.EndsWithParam}\");".Replace("{col}", col));
                sb.Append($" p[\"{field.EndsWithParam}\"] = \"%\" + FilterHelpers.EscapeLike({readMethod});");
                sb.AppendLine(" },");

                sb.Append($"        (sql, r, p) => {{ ");
                sb.Append($" sql.Append(\"{col} {config.LikeOperator} {field.ContainsParam}\");".Replace("{col}", col));
                sb.Append($" p[\"{field.ContainsParam}\"] = \"%\" + FilterHelpers.EscapeLike({readMethod}) + \"%\";");
                sb.AppendLine(" },");
            }

            // IN and NOT IN operators with dedicated parameter names
            if (field.FieldType != QueryFieldType.Boolean)
            {
                GenerateArrayOperatorHandler(sb, field.InId, field.PropertyName, fieldNameLower, "IN", config.InOperator, col, readMethod, config, field.BaseType, ref dynamicParamIndex);
                GenerateArrayOperatorHandler(sb, field.NotInId, field.PropertyName, fieldNameLower, "NOT IN", config.NotInOperator, col, readMethod, config, field.BaseType, ref dynamicParamIndex);
            }
        }

        sb.AppendLine("    };");
        sb.AppendLine();

        // Generate filter write jump table
        sb.AppendLine("    private delegate void FilterWriteHandler(BinaryWriter writer, object value);");
        sb.AppendLine("    ");
        sb.AppendLine("    private static readonly FilterWriteHandler[] FilterWriteJumpTable =");
        sb.AppendLine("    {");

        foreach (var field in paramMap.FieldOperators)
        {
            var writeMethod = Helpers.GetBinaryWriteMethod(field.BaseType);

            sb.AppendLine("        (w, v) => w.Write((bool)v),");
            sb.AppendLine("        (w, v) => w.Write((bool)v),");
            sb.AppendLine($"        (w, v) => {writeMethod},");
            sb.AppendLine($"        (w, v) => {writeMethod},");

            if (field.GtId >= 0)
            {
                sb.AppendLine($"        (w, v) => {writeMethod},");
                sb.AppendLine($"        (w, v) => {writeMethod},");
                sb.AppendLine($"        (w, v) => {writeMethod},");
                sb.AppendLine($"        (w, v) => {writeMethod},");
            }

            if (field.StartsWithId >= 0)
            {
                sb.AppendLine($"        (w, v) => {writeMethod},");
                sb.AppendLine($"        (w, v) => {writeMethod},");
                sb.AppendLine($"        (w, v) => {writeMethod},");
            }

            if (field.FieldType != QueryFieldType.Boolean)
            {
                var arrayWriteMethod = Helpers.GetArrayWriteMethod(field.BaseType);
                sb.AppendLine($"        {arrayWriteMethod},");
                sb.AppendLine($"        {arrayWriteMethod},");
            }
        }

        sb.AppendLine("    };");
        sb.AppendLine();

        // Generate cursor jump table
        var idProp = entityInfo.SortableProperties.First(p => p.Name == entityInfo.IdentifierProperty.Name);
        var idCol = Helpers.QuoteIdentifier(idProp.GetFullColumnName(), config.IdentifierQuote);
        var idReadMethod = Helpers.GetBinaryReadMethod(idProp.BaseType);
        var otherSortables = entityInfo.SortableProperties.Where(p => p.Name != entityInfo.IdentifierProperty.Name).ToList();

        sb.AppendLine("    private static readonly Action<StringBuilder, BinaryReader, Dictionary<string, object>>[] CursorJumpTable =");
        sb.AppendLine("    {");

        byte sortId = 0;
        sb.AppendLine($"        (sql, r, p) => {{ sql.Append(\"{idCol} > {config.ParameterPrefix}cursor_id\"); p[\"{config.ParameterPrefix}cursor_id\"] = {idReadMethod}; }},");
        sortId++;

        sb.AppendLine($"        (sql, r, p) => {{ sql.Append(\"{idCol} < {config.ParameterPrefix}cursor_id\"); p[\"{config.ParameterPrefix}cursor_id\"] = {idReadMethod}; }},");
        sortId++;

        foreach (var prop in otherSortables)
        {
            var col = Helpers.QuoteIdentifier(prop.GetFullColumnName(), config.IdentifierQuote);
            var readMethod = Helpers.GetBinaryReadMethod(prop.BaseType);

            sb.Append($"        (sql, r, p) => {{ ");
            sb.Append($"sql.Append(\"({col} > {config.ParameterPrefix}cursor_sort) OR ({col} = {config.ParameterPrefix}cursor_sort AND {idCol} > {config.ParameterPrefix}cursor_id)\");");
            sb.Append($" p[\"{config.ParameterPrefix}cursor_sort\"] = {readMethod};");
            sb.Append($" p[\"{config.ParameterPrefix}cursor_id\"] = {idReadMethod};");
            sb.AppendLine(" },");
            sortId++;

            sb.Append($"        (sql, r, p) => {{ ");
            sb.Append($"sql.Append(\"({col} < {config.ParameterPrefix}cursor_sort) OR ({col} = {config.ParameterPrefix}cursor_sort AND {idCol} > {config.ParameterPrefix}cursor_id)\");");
            sb.Append($" p[\"{config.ParameterPrefix}cursor_sort\"] = {readMethod};");
            sb.Append($" p[\"{config.ParameterPrefix}cursor_id\"] = {idReadMethod};");
            sb.AppendLine(" },");
            sortId++;
        }

        sb.AppendLine("    };");
        sb.AppendLine();

        // Generate ORDER BY strings
        sb.AppendLine("    private static readonly string[] OrderByStrings = ");
        sb.AppendLine("    {");

        sortId = 0;
        sb.AppendLine($"        \"{idCol} ASC\",");
        sb.AppendLine($"        \"{idCol} DESC\",");

        foreach (var prop in otherSortables)
        {
            var col = Helpers.QuoteIdentifier(prop.GetFullColumnName(), config.IdentifierQuote);
            sb.AppendLine($"        \"{col} ASC, {idCol} ASC\",");
            sb.AppendLine($"        \"{col} DESC, {idCol} ASC\",");
        }

        sb.AppendLine("    };");
    }

    private static void GenerateArrayOperatorHandler(StringBuilder sb, int opId, string propName, string fieldNameLower,
        string opName, string sqlOp, string col, string readMethod, SqlProviderConfig config, string baseType, ref int dynamicParamIndex)
    {

        if (config.UseArrayForIn)
        {
            var paramName = $"{config.ParameterPrefix}{fieldNameLower}{opName.Replace(" ", "")}";
            sb.AppendLine("        (sql, r, p) => {");
            sb.AppendLine("            var arrLen = r.ReadInt32();");
            sb.AppendLine("            var arr = new object[arrLen];");
            sb.AppendLine($"            for (int j = 0; j < arrLen; j++) arr[j] = {readMethod};");
            sb.AppendLine($"            sql.Append(\"{col} {sqlOp} ({paramName})\");".Replace("{col}", col));
            sb.AppendLine($"            p[\"{paramName}\"] = arr;");
            sb.AppendLine("        },");
        }
        else
        {
            sb.AppendLine("        (sql, r, p) => {");
            sb.AppendLine("            var arrLen = r.ReadInt32();");
            sb.AppendLine($"            sql.Append(\"{col} {sqlOp} (\");".Replace("{col}", col));
            sb.AppendLine("            for (int j = 0; j < arrLen; j++)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (j > 0) sql.Append(\", \");");
            sb.AppendLine($"                var paramName = $\"{config.ParameterPrefix}{fieldNameLower}{(opName == "IN" ? "In" : "NotIn")}{{j}}\";");
            sb.AppendLine("                sql.Append(paramName);");
            sb.AppendLine($"                p[paramName] = {readMethod};");
            sb.AppendLine("            }");
            sb.AppendLine("            sql.Append(')');");
            sb.AppendLine("        },");
        }

        dynamicParamIndex++;
    }

    private static void GenerateHelperMethods(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config, int runtimeParamStart)
    {
        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    private static void AppendFilterConditions(StringBuilder sql, BinaryReader reader, Dictionary<string, object> parameters, int filterCount)");
        sb.AppendLine("    {");
        sb.AppendLine("        for (int i = 0; i < filterCount; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (i > 0) sql.Append(\" AND \");");
        sb.AppendLine("            var fieldOpId = reader.ReadByte();");
        sb.AppendLine("            ");
        sb.AppendLine("            if (fieldOpId < FilterReadJumpTable.Length)");
        sb.AppendLine("                FilterReadJumpTable[fieldOpId](sql, reader, parameters);");
        sb.AppendLine("            else");
        sb.AppendLine("                throw new ArgumentException($\"Invalid fieldOpId: {fieldOpId}\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    private static void AppendOrderBy(StringBuilder sql, byte sortFieldDir)");
        sb.AppendLine("    {");
        sb.AppendLine("        sql.Append(\" ORDER BY \");");
        sb.AppendLine("        if (sortFieldDir < OrderByStrings.Length)");
        sb.AppendLine("            sql.Append(OrderByStrings[sortFieldDir]);");
        sb.AppendLine("        else");
        sb.AppendLine("            throw new ArgumentException($\"Invalid sortFieldDir: {sortFieldDir}\");");
        sb.AppendLine("    }");
    }

    private static void GenerateEncodeCursor(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config, int runtimeParamStart)
    {
        var idProp = entityInfo.SortableProperties.First(p => p.Name == entityInfo.IdentifierProperty.Name);
        var otherSortables = entityInfo.SortableProperties.Where(p => p.Name != entityInfo.IdentifierProperty.Name).ToList();

        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveOptimization)]");
        sb.AppendLine($"    public static string EncodeCursor({entityInfo.ResponseTypeName} item, byte[] filterSortBytes, byte sortFieldDir)");
        sb.AppendLine("    {");
        sb.AppendLine("        using var ms = new MemoryStream(filterSortBytes.Length + 64);");
        sb.AppendLine("        using var writer = new BinaryWriter(ms);");
        sb.AppendLine();
        sb.AppendLine("        writer.Write(filterSortBytes);");
        sb.AppendLine();
        sb.AppendLine("        switch (sortFieldDir)");
        sb.AppendLine("        {"); ;

        byte sortId = 0;
        sb.AppendLine($"            case {sortId++}:");
        sb.AppendLine($"            case {sortId++}:");
        sb.AppendLine("                writer.Write((byte)1);");
        GenerateWriteValue(sb, idProp, $"item.{idProp.Name}");
        sb.AppendLine("                break;");

        foreach (var prop in otherSortables)
        {
            sb.AppendLine($"            case {sortId++}:");
            sb.AppendLine($"            case {sortId++}:");
            sb.AppendLine("                writer.Write((byte)2);");
            GenerateWriteValue(sb, prop, $"item.{prop.Name}");
            GenerateWriteValue(sb, idProp, $"item.{idProp.Name}");
            sb.AppendLine("                break;");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return CursorSecurity.SignCursor(ms.ToArray());");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Decodes the LastModifiedAt + Id pair out of a cursor that was built from
    /// an UNFILTERED query sorted by LastModifiedAt -- the shape every sync
    /// cursor uses (see EncodeCursor: filterCount is 0 when no Filter was
    /// supplied, so the only bytes before the sort key are TypeHashCode and a
    /// single zero filterCount byte). Any other cursor shape (filtered, or
    /// sorted by a different field) returns null rather than guessing --
    /// callers must treat null as "can't tell, assume changed."
    /// </summary>
    private static void GenerateDecodeLastModified(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config)
    {
        var idProp = entityInfo.SortableProperties.First(p => p.Name == entityInfo.IdentifierProperty.Name);
        var otherSortables = entityInfo.SortableProperties.Where(p => p.Name != entityInfo.IdentifierProperty.Name).ToList();
        var lastModifiedIndex = otherSortables.FindIndex(p => p.Name == "LastModifiedAt");

        sb.AppendLine("    /// <summary>Sync-preflight helper -- see GenerateDecodeLastModified. Returns null for any cursor that isn't an unfiltered, LastModifiedAt-sorted sync cursor.</summary>");
        sb.AppendLine("    public static (DateTime LastModifiedAt, Guid Id)? DecodeLastModifiedAt(string cursor)");
        sb.AppendLine("    {");

        if (lastModifiedIndex < 0 || idProp.BaseType != "System.Guid")
        {
            // This entity either has no LastModifiedAt in its sortable set, or
            // its identifier isn't a Guid (TryReadGuidId on the interceptor
            // side only ever produces Guids) -- sync preflight can't use this
            // entity's cursor either way, so the decoder is a stable no-op.
            sb.AppendLine("        return null;");
            sb.AppendLine("    }");
            return;
        }

        sb.AppendLine("        if (!CursorSecurity.VerifyAndExtract(cursor, out var cursorData))");
        sb.AppendLine("            return null;");
        sb.AppendLine();
        sb.AppendLine("        using var ms = new MemoryStream(cursorData);");
        sb.AppendLine("        using var reader = new BinaryReader(ms);");
        sb.AppendLine();
        sb.AppendLine("        if (reader.ReadInt32() != TypeHashCode)");
        sb.AppendLine("            return null;");
        sb.AppendLine();
        sb.AppendLine("        if (reader.ReadByte() != 0)");
        sb.AppendLine("            return null; // filtered cursor -- not the plain sync shape this decodes");
        sb.AppendLine();
        sb.AppendLine("        var sortFieldDir = reader.ReadByte();");

        var lastModifiedDirLow = (byte)(2 + 2 * lastModifiedIndex);
        var lastModifiedDirHigh = (byte)(lastModifiedDirLow + 1);

        sb.AppendLine($"        if (sortFieldDir != {lastModifiedDirLow} && sortFieldDir != {lastModifiedDirHigh})");
        sb.AppendLine("            return null; // not sorted by LastModifiedAt -- not the plain sync shape this decodes");
        sb.AppendLine();
        sb.AppendLine("        reader.ReadByte(); // discard EncodeCursor's sort-key-count marker (always 2 for a non-identifier sort field)");
        sb.AppendLine("        var lastModifiedAt = DateTime.FromBinary(reader.ReadInt64());");
        sb.AppendLine("        var id = new Guid(reader.ReadBytes(16));");
        sb.AppendLine("        return (lastModifiedAt, id);");
        sb.AppendLine("    }");
    }

    private static void GeneratePrepareFilterSortBytes(StringBuilder sb, EntityInfo entityInfo, SqlProviderConfig config, ParameterMap paramMap)
    {
        var idProp = entityInfo.SortableProperties.First(p => p.Name == entityInfo.IdentifierProperty.Name);
        var otherSortables = entityInfo.SortableProperties.Where(p => p.Name != entityInfo.IdentifierProperty.Name).ToList();

        sb.AppendLine("        using var writerMs = new MemoryStream(256);");
        sb.AppendLine("        using var writer = new BinaryWriter(writerMs);");
        sb.AppendLine();
        sb.AppendLine("        writer.Write(TypeHashCode);");
        sb.AppendLine();
        sb.AppendLine("        if(filter != null) {");
        sb.AppendLine("            var filterList = new List<(byte fieldOpId, object value)>(16);");
        sb.AppendLine();

        foreach (var prop in entityInfo.FilterableProperties)
        {
            var propName = prop.Name;
            var fieldIdx = entityInfo.FilterableProperties.IndexOf(prop);
            var fieldInfo = paramMap.FieldOperators[fieldIdx];

            sb.AppendLine($"            if (filter?.{propName} != null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                var f = filter.{propName}.Value;");

            sb.AppendLine($"                if (f.IsNull.HasValue) filterList.Add((f.IsNull.Value ? (byte){fieldInfo.IsNullTrueId} : (byte){fieldInfo.IsNullFalseId}, f.IsNull.Value));");

            var isString = prop.FieldType == QueryFieldType.String;
            var nullCheck = isString ? "!string.IsNullOrWhiteSpace(f.Eq)" : "f.Eq.HasValue";
            var valueAccess = isString ? "f.Eq!" : "f.Eq.Value";

            sb.AppendLine($"                if ({nullCheck}) filterList.Add(({fieldInfo.EqId}, (object){valueAccess}));");
            sb.AppendLine($"                if ({nullCheck.Replace("Eq", "Ne")}) filterList.Add(({fieldInfo.NeId}, (object){valueAccess.Replace("Eq", "Ne")}));");

            if (prop.FieldType == QueryFieldType.Number || prop.FieldType == QueryFieldType.DateTime || prop.FieldType == QueryFieldType.DateOnly)
            {
                sb.AppendLine($"                if (f.Gt.HasValue) filterList.Add(((byte){fieldInfo.GtId}, (object)f.Gt.Value));");
                sb.AppendLine($"                if (f.Gte.HasValue) filterList.Add(((byte){fieldInfo.GteId}, (object)f.Gte.Value));");
                sb.AppendLine($"                if (f.Lt.HasValue) filterList.Add(((byte){fieldInfo.LtId}, (object)f.Lt.Value));");
                sb.AppendLine($"                if (f.Lte.HasValue) filterList.Add(((byte){fieldInfo.LteId}, (object)f.Lte.Value));");
            }

            if (prop.FieldType == QueryFieldType.String)
            {
                sb.AppendLine($"                if (!string.IsNullOrWhiteSpace(f.StartsWith)) filterList.Add(((byte){fieldInfo.StartsWithId}, (object)f.StartsWith!));");
                sb.AppendLine($"                if (!string.IsNullOrWhiteSpace(f.EndsWith)) filterList.Add(((byte){fieldInfo.EndsWithId}, (object)f.EndsWith!));");
                sb.AppendLine($"                if (!string.IsNullOrWhiteSpace(f.Contains)) filterList.Add(((byte){fieldInfo.ContainsId}, (object)f.Contains!));");
            }

            if (prop.FieldType != QueryFieldType.Boolean)
            {
                sb.AppendLine($"                if (f.In != null && f.In.Length > 0) filterList.Add(((byte){fieldInfo.InId}, (object)f.In));");
                sb.AppendLine($"                if (f.NotIn != null && f.NotIn.Length > 0) filterList.Add(((byte){fieldInfo.NotInId}, (object)f.NotIn));");
            }

            sb.AppendLine("            }");
            sb.AppendLine();
        }

        sb.AppendLine("            writer.Write((byte)filterList.Count);");
        sb.AppendLine("                ");
        sb.AppendLine("            foreach (var (fieldOpId, value) in CollectionsMarshal.AsSpan(filterList))");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.Write(fieldOpId);");
        sb.AppendLine("                if (fieldOpId >= FilterWriteJumpTable.Length)");
        sb.AppendLine("                    throw new ArgumentOutOfRangeException(nameof(fieldOpId), $\"Invalid fieldOpId: {fieldOpId}\");");
        sb.AppendLine("                FilterWriteJumpTable[fieldOpId](writer, value);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        else {");
        sb.AppendLine("            writer.Write((byte)0);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Generate sortFieldDir calculation
        sb.AppendLine("        var sortFieldDir = sortBy switch");
        sb.AppendLine("        {");

        byte sortId = 0;
        var idName = idProp.Name;
        sb.AppendLine($"            \"{idName}\" or \"\" or null => sortDesc ? (byte){sortId + 1} : (byte){sortId},");
        sortId += 2;

        foreach (var prop in otherSortables)
        {
            var propName = prop.Name;
            sb.AppendLine($"            \"{propName}\" => sortDesc ? (byte){sortId + 1} : (byte){sortId},");
            sortId += 2;
        }

        sb.AppendLine("            _ => throw new ArgumentException($\"Invalid SortBy field: {sortBy}\")");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        writer.Write(sortFieldDir);");
        sb.AppendLine("        var filterSortBytes = writerMs.ToArray();");
    }

    private static void GenerateWriteValue(StringBuilder sb, PropertyInfo prop, string valueExpr)
    {
        var isNullableValueType = prop.TypeName.EndsWith('?') && prop.BaseType != "string" && prop.BaseType != "System.String";
        var valueAccess = isNullableValueType ? $"({valueExpr}).GetValueOrDefault()" : $"({valueExpr})";

        if (prop.BaseType == "System.DateTime")
            sb.AppendLine($"                writer.Write({valueAccess}.ToBinary());");
        else if (prop.BaseType == "System.DateOnly")
            sb.AppendLine($"                writer.Write({valueAccess}.DayNumber);");
        else if (prop.BaseType == "System.Guid")
            sb.AppendLine($"                writer.Write({valueAccess}.ToByteArray());");
        else if (prop.FieldType == QueryFieldType.Enum)
            sb.AppendLine($"                writer.Write((int)({valueAccess}));");
        else if (prop.BaseType == "string" || prop.BaseType == "System.String")
            sb.AppendLine($"                writer.Write({valueAccess} ?? string.Empty);");
        else
            sb.AppendLine($"                writer.Write({valueAccess});");
    }
}
