namespace CursorPaginator.Generator;

internal static class Extensions
{
    public static StringBuilder AppendLine(this StringBuilder sb, string text, int indent = 0, char indentChar = ' ')
    {
        if (indent > 0)
        {
            sb.Append(new string(indentChar, indent));
        }

        sb.AppendLine(text);
        return sb;
    }

    public static string Capitalize(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToUpperInvariant(input[0]) + input[1..].ToLowerInvariant();
    }

    public static string ConvertNaming(this string input, NamingConvention convention)
    {
        if (string.IsNullOrEmpty(input)) return input;

        return convention switch
        {
            NamingConvention.SnakeCase => ConvertToSnakeCase(input),
            NamingConvention.CamelCase => ConvertToCamelCase(input),
            NamingConvention.PascalCase => input,
            _ => input
        };
    }

    private static string ConvertToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var builder = new StringBuilder(input.Length + Math.Min(2, input.Length / 5));
        var previousCategory = default(UnicodeCategory?);

        for (var currentIndex = 0; currentIndex < input.Length; currentIndex++)
        {
            var currentChar = input[currentIndex];
            if (currentChar == '_')
            {
                builder.Append('_');
                previousCategory = null;
                continue;
            }

            var currentCategory = char.GetUnicodeCategory(currentChar);
            switch (currentCategory)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                    if (previousCategory == UnicodeCategory.SpaceSeparator ||
                        previousCategory == UnicodeCategory.LowercaseLetter ||
                        (previousCategory != UnicodeCategory.DecimalDigitNumber &&
                         previousCategory != null &&
                         currentIndex > 0 &&
                         currentIndex + 1 < input.Length &&
                         char.IsLower(input[currentIndex + 1])))
                    {
                        builder.Append('_');
                    }

                    currentChar = char.ToLowerInvariant(currentChar);
                    break;

                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.DecimalDigitNumber:
                    if (previousCategory == UnicodeCategory.SpaceSeparator)
                    {
                        builder.Append('_');
                    }
                    break;

                default:
                    if (previousCategory != null)
                    {
                        previousCategory = UnicodeCategory.SpaceSeparator;
                    }
                    continue;
            }

            builder.Append(currentChar);
            previousCategory = currentCategory;
        }

        return builder.ToString();
    }

    private static string ConvertToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToLowerInvariant(input[0]) + input[1..];
    }

    public static T? GetAttributeValue<T>(this AttributeData attribute, int index, T? defaultValue = default)
    {
        if (attribute.ConstructorArguments.Length <= index)
            return defaultValue;

        var arg = attribute.ConstructorArguments[index];
        if (arg.IsNull)
            return defaultValue;

        return arg.Value is T typedValue ? typedValue : defaultValue;
    }

    public static T? GetNamedAttributeValue<T>(this AttributeData attribute, string name, T? defaultValue = default)
    {
        var namedArg = attribute.NamedArguments.FirstOrDefault(x => x.Key == name);
        if (namedArg.Equals(default(KeyValuePair<string, TypedConstant>)))
            return defaultValue;

        var constant = namedArg.Value;

        if (constant.IsNull)
            return defaultValue;

        if (constant.Kind == TypedConstantKind.Array)
        {
            if (typeof(T).IsArray)
            {
                var elementType = typeof(T).GetElementType();
                if (elementType == typeof(string))
                    return (T)(object)constant.Values.Select(v => v.Value?.ToString()).ToArray();
                if (elementType == typeof(int))
                    return (T)(object)constant.Values.Select(v => Convert.ToInt32(v.Value)).ToArray();
                if (elementType == typeof(bool))
                    return (T)(object)constant.Values.Select(v => Convert.ToBoolean(v.Value)).ToArray();
            }

            return defaultValue;
        }

        return constant.Value is T typedValue ? typedValue : defaultValue;
    }
}
