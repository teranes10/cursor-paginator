namespace CursorPaginator.Generator.Core;

internal static class SqlExpressionValidator
{
    private static readonly HashSet<string> AllowedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SUM", "COUNT", "AVG", "MIN", "MAX", "COALESCE", "NULLIF",
        "CASE", "WHEN", "THEN", "ELSE", "END", "CAST", "AS",
        "AND", "OR", "NOT", "IS", "NULL"
    };

    private static readonly char[] AllowedOperators = ['+', '-', '*', '/', '%', '(', ')', ',', '.', ' '];

    public static bool IsValidExpression(string expression, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "Expression cannot be empty";
            return false;
        }

        // Check for dangerous SQL keywords
        var dangerous = new[] { "DROP", "DELETE", "INSERT", "UPDATE", "ALTER", "CREATE", "EXEC", "EXECUTE", "--", "/*", "*/", ";" };
        foreach (var keyword in dangerous)
        {
            if (expression.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Expression contains dangerous keyword: {keyword}";
                return false;
            }
        }

        // Basic validation: only allow identifiers, numbers, operators, and whitelisted functions
        var tokens = expression.Split(AllowedOperators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token)) continue;

            // Check if it's a number
            if (decimal.TryParse(token, out _)) continue;

            // Check if it's an allowed keyword
            if (AllowedKeywords.Contains(token)) continue;

            // Check if it's a valid identifier (column name)
            if (IsValidIdentifier(token)) continue;

            error = $"Invalid token in expression: {token}";
            return false;
        }

        return true;
    }

    private static bool IsValidIdentifier(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;

        // Must start with letter or underscore
        if (!char.IsLetter(token[0]) && token[0] != '_') return false;

        // Rest must be letters, digits, or underscores
        for (int i = 1; i < token.Length; i++)
        {
            char c = token[i];
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                return false;
        }

        return true;
    }
}
