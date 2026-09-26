namespace CursorPaginator.Generator.Core;

internal sealed class ComputedFieldInfo
{
    public required string Name { get; init; }
    public required string Expression { get; init; }
    public bool Filterable { get; init; }
    public bool Sortable { get; init; }
    public required string TypeName { get; init; } = "";
    public string BaseType { get; init; } = "";

    public static (ComputedFieldInfo? Computed, Diagnostic? Diagnostic) From(IPropertySymbol propSymbol)
    {
        var computedAttr = propSymbol.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.Name == "ComputedAttribute");
        if (computedAttr == null) return (null, null);

        try
        {
            var expression = computedAttr.GetNamedAttributeValue("Expression", "")!;
            if (!SqlExpressionValidator.IsValidExpression(expression, out var error))
            {
                return (null, Diagnostic.Create(
                    DiagnosticDescriptors.InvalidComputedExpression,
                    propSymbol.Locations.FirstOrDefault(),
                    propSymbol.Name,
                    error
                ));
            }

            var filterable = computedAttr.GetNamedAttributeValue("Filterable", false);
            var sortable = computedAttr.GetNamedAttributeValue("Sortable", false);
            var type = propSymbol.Type.ToDisplayString();
            var baseType = type.TrimEnd('?');

            return (new ComputedFieldInfo
            {
                Name = propSymbol.Name,
                Expression = expression,
                Filterable = filterable,
                Sortable = sortable,
                TypeName = type,
                BaseType = baseType
            }, null);
        }
        catch
        {
            return (null, null);
        }
    }
}