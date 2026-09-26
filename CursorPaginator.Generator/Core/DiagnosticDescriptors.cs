namespace CursorPaginator.Generator.Core;

internal static class DiagnosticDescriptors
{
    private const string Category = "CursorPaginator";

    public static readonly DiagnosticDescriptor InvalidComputedExpression = new(
        id: "CP0001",
        title: "Invalid computed field expression",
        messageFormat: "Computed field '{0}' contains invalid SQL expression: {1}",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingTableName = new(
        id: "CP0002",
        title: "Missing table name",
        messageFormat: "GenerateQuery attribute on '{0}' must specify a table name",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TooManySortableFields = new(
        id: "CP0003",
        title: "Too many sortable fields",
        messageFormat: "Type '{0}' has {1} sortable fields, maximum is {2}",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidAttributeUsage = new(
        id: "CP0004",
        title: "Invalid attribute usage",
        messageFormat: "Attribute '{0}' on property '{1}' is invalid: {2}",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidEntityType = new(
        id: "CP005",
        title: "Invalid Entity type",
        messageFormat: "Failed to generate code for '{0}': {1}",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingResponseType = new(
        id: "CP0006",
        title: "Missing response type",
        messageFormat: "GenerateQuery attribute on '{0}' must specify a Response",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingFilterType = new(
        id: "CP0006",
        title: "Missing filter type",
        messageFormat: "GenerateQuery attribute on '{0}' must specify a Filter",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GenerationError = new(
        id: "CP0099",
        title: "Code generation error",
        messageFormat: "Failed to generate code for '{0}': {1}",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
