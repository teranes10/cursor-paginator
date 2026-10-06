namespace CursorPaginator.Generator.Core;

internal class ParameterMap
{
    public List<FieldOperatorInfo> FieldOperators { get; } = [];
    public int RuntimeParamStart { get; set; }

    public static ParameterMap CalculateParameterMap(EntityInfo entityInfo)
    {
        var map = new ParameterMap();
        var currentParam = 0;

        foreach (var prop in entityInfo.FilterableProperties)
        {
            var info = new FieldOperatorInfo
            {
                PropertyName = prop.Name,
                BaseType = prop.BaseType,
                FieldType = prop.FieldType,
                EnumAsString = prop.EnumAsString,
                ColumnName = prop.GetFullColumnName(),
                IsNullTrueId = currentParam++,
                IsNullFalseId = currentParam++,
                EqId = currentParam++,
                NeId = currentParam++,
                GtId = -1,
                GteId = -1,
                LtId = -1,
                LteId = -1,
                StartsWithId = -1,
                EndsWithId = -1,
                ContainsId = -1
            };

            if (prop.FieldType == QueryFieldType.Number || prop.FieldType == QueryFieldType.DateTime || prop.FieldType == QueryFieldType.DateOnly)
            {
                info.GtId = currentParam++;
                info.GteId = currentParam++;
                info.LtId = currentParam++;
                info.LteId = currentParam++;
            }

            if (prop.FieldType == QueryFieldType.String)
            {
                info.StartsWithId = currentParam++;
                info.EndsWithId = currentParam++;
                info.ContainsId = currentParam++;
            }

            if (prop.FieldType != QueryFieldType.Boolean)
            {
                info.InId = currentParam++;
                info.NotInId = currentParam++;
            }

            map.FieldOperators.Add(info);
        }

        // Round up to next multiple of 10 for clean parameter spacing
        map.RuntimeParamStart = ((currentParam + 9) / 10) * 10;
        return map;
    }
}
