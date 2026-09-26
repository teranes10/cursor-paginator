namespace CursorPaginator.Generator.Generators;

internal sealed class SqlQueryBuilder
{
    public static string BuildFromAndJoins(EntityInfo entityInfo, SqlProviderConfig config)
    {
        var sb = new StringBuilder();
        sb.Append("FROM ");
        sb.Append(Helpers.QuoteIdentifier(entityInfo.TableName, config.IdentifierQuote));

        if (entityInfo.Joins.Count > 0)
        {
            foreach (var join in entityInfo.Joins)
            {
                sb.AppendLine();
                sb.Append(BuildJoinClause(join, entityInfo, config));
            }
        }

        return sb.ToString();
    }

    public static string BuildBaseQuery(EntityInfo entityInfo, SqlProviderConfig config)
    {
        var sb = new StringBuilder(GeneratorConstants.DefaultStringBuilderCapacity);
        var selectFields = BuildSelectFields(entityInfo, config);

        sb.Append("SELECT ");

        if (selectFields.Count > 0)
        {
            sb.Append(selectFields[0]);
            for (int i = 1; i < selectFields.Count; i++)
            {
                sb.AppendLine(",");
                sb.Append('\t');
                sb.Append(selectFields[i]);
            }
        }

        sb.AppendLine();
        sb.Append(BuildFromAndJoins(entityInfo, config));

        if (entityInfo.HasTenantFilter || entityInfo.WhereClauses.Count > 0)
        {
            sb.AppendLine();
            sb.Append("WHERE ");
            
            bool hasTenant = false;
            if (entityInfo.HasTenantFilter)
            {
                sb.Append($"{Helpers.QuoteIdentifier(entityInfo.TableName, config.IdentifierQuote)}.{Helpers.QuoteIdentifier("tenant_id", config.IdentifierQuote)} = @TenantId");
                hasTenant = true;
            }

            if (entityInfo.WhereClauses.Count > 0)
            {
                if (hasTenant)
                {
                    sb.Append(" AND ");
                }
                sb.Append(BuildWhereClause(entityInfo.WhereClauses, entityInfo.NamingConvention, config, entityInfo.TableName));
            }
        }

        return sb.ToString();
    }

    private static List<string> BuildSelectFields(EntityInfo entityInfo, SqlProviderConfig config)
    {
        var selectFields = new List<string>();

        foreach (var prop in entityInfo.SelectProperties)
        {
            var fullCol = Helpers.QuoteIdentifier(prop.GetFullColumnName(), config.IdentifierQuote);
            var expectedPropCol = prop.Name.ConvertNaming(entityInfo.NamingConvention);
            if (!string.Equals(prop.ColumnName, expectedPropCol, StringComparison.OrdinalIgnoreCase))
            {
                selectFields.Add($"{fullCol} AS {Helpers.QuoteIdentifier(expectedPropCol, config.IdentifierQuote)}");
            }
            else
            {
                selectFields.Add(fullCol);
            }
        }

        foreach (var join in entityInfo.Joins)
        {
            if (join.SelectedColumns.Length > 0)
            {
                selectFields.AddRange(
                    join.SelectedColumns.Select(col =>
                        $"{Helpers.QuoteIdentifier(join.Alias, config.IdentifierQuote)}.{Helpers.QuoteIdentifier(col.ConvertNaming(entityInfo.NamingConvention), config.IdentifierQuote)}")
                );
            }
        }

        selectFields.AddRange(
            entityInfo.ComputedFields.Select(computed =>
                $"({computed.Expression}) AS {Helpers.QuoteIdentifier(computed.Name.ConvertNaming(entityInfo.NamingConvention), config.IdentifierQuote)}")
        );

        return selectFields;
    }

    private static string BuildJoinClause(JoinInfo join, EntityInfo entityInfo, SqlProviderConfig config)
    {
        var sb = new StringBuilder();

        sb.Append(GetJoinTypeString(join.Type));
        sb.Append(' ');
        sb.Append(Helpers.QuoteIdentifier(join.Table, config.IdentifierQuote));

        if (!string.IsNullOrEmpty(join.Alias) && join.Alias != join.Table)
        {
            sb.Append(" AS ");
            sb.Append(Helpers.QuoteIdentifier(join.Alias, config.IdentifierQuote));
        }

        sb.Append(" ON ");

        for (int i = 0; i < join.Conditions.Count; i++)
        {
            var condition = join.Conditions[i];

            if (i > 0)
            {
                sb.Append(' ');
                sb.Append(GetLogicalOperatorString(condition.LogicalOperator));
                sb.Append(' ');
            }

            sb.Append(Helpers.QuoteIdentifier(condition.LeftTable, config.IdentifierQuote));
            sb.Append('.');
            sb.Append(Helpers.QuoteIdentifier(condition.LeftColumn.ConvertNaming(entityInfo.NamingConvention), config.IdentifierQuote));
            sb.Append(' ');
            sb.Append(GetClauseOperatorString(condition.Operator));
            sb.Append(' ');
            sb.Append(Helpers.QuoteIdentifier(condition.RightTable, config.IdentifierQuote));
            sb.Append('.');
            sb.Append(Helpers.QuoteIdentifier(condition.RightColumn.ConvertNaming(entityInfo.NamingConvention), config.IdentifierQuote));
        }

        if (join.WhereClauses.Count > 0)
        {
            sb.Append(" AND ");
            sb.Append(BuildWhereClause(join.WhereClauses, entityInfo.NamingConvention, config, join.Alias));
        }

        return sb.ToString();
    }

    internal static string BuildWhereClause(List<WhereClause> clauses, NamingConvention namingConvention, SqlProviderConfig config, string? defaultTable = null)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < clauses.Count; i++)
        {
            var clause = clauses[i];

            if (i > 0)
            {
                sb.Append(' ');
                sb.Append(GetLogicalOperatorString(clause.LogicalOperator));
                sb.Append(' ');
            }

            var table = !string.IsNullOrEmpty(clause.Table) ? clause.Table : defaultTable;
            if (!string.IsNullOrEmpty(table))
            {
                sb.Append(Helpers.QuoteIdentifier(table, config.IdentifierQuote));
                sb.Append('.');
            }
            sb.Append(Helpers.QuoteIdentifier(clause.Property.ConvertNaming(namingConvention), config.IdentifierQuote));
            sb.Append(' ');
            sb.Append(GetClauseOperatorString(clause.Operator));

            if (clause.Operator != FilterOperator.IsNull)
            {
                sb.Append(' ');

                if (!string.IsNullOrEmpty(clause.CompareToProperty))
                {
                    sb.Append(Helpers.QuoteIdentifier(clause.CompareToProperty.ConvertNaming(namingConvention), config.IdentifierQuote));
                }
                else if (clause.Value != null)
                {
                    sb.Append(FormatValue(clause.Value, config));
                }
            }
        }

        return sb.ToString();
    }

    private static string GetJoinTypeString(JoinType joinType) => joinType switch
    {
        JoinType.Inner => "INNER JOIN",
        JoinType.Left => "LEFT JOIN",
        JoinType.Right => "RIGHT JOIN",
        JoinType.Full => "FULL OUTER JOIN",
        _ => "LEFT JOIN"
    };

    private static string GetClauseOperatorString(FilterOperator op) => op switch
    {
        FilterOperator.Eq => "=",
        FilterOperator.Ne => "!=",
        FilterOperator.Gt => ">",
        FilterOperator.Gte => ">=",
        FilterOperator.Lt => "<",
        FilterOperator.Lte => "<=",
        FilterOperator.In => "IN",
        FilterOperator.NotIn => "NOT IN",
        FilterOperator.IsNull => "IS NULL",
        _ => "="
    };

    private static string GetLogicalOperatorString(LogicalOperator op) => op switch
    {
        LogicalOperator.And => "AND",
        LogicalOperator.Or => "OR",
        _ => "AND"
    };

    private static string FormatValue(object value, SqlProviderConfig config)
    {
        return value switch
        {
            bool b => config.DatabaseProvider == DatabaseProvider.PostgreSQL ? (b ? "true" : "false") : (b ? "1" : "0"),
            string s => $"'{s}'",
            null => "NULL",
            _ => value.ToString() ?? "NULL"
        };
    }
}