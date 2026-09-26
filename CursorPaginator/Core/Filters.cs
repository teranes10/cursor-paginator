using System.Runtime.CompilerServices;

namespace CursorPaginator.Core;

public readonly struct StringFilter
{
    public bool? IsNull { get; init; }
    public string? Eq { get; init; }
    public string? Ne { get; init; }
    public string? StartsWith { get; init; }
    public string? EndsWith { get; init; }
    public string? Contains { get; init; }
    public string[]? In { get; init; }
    public string[]? NotIn { get; init; }
}

public readonly struct NumberFilter<T> where T : struct
{
    public bool? IsNull { get; init; }
    public T? Eq { get; init; }
    public T? Ne { get; init; }
    public T? Gt { get; init; }
    public T? Gte { get; init; }
    public T? Lt { get; init; }
    public T? Lte { get; init; }
    public T[]? In { get; init; }
    public T[]? NotIn { get; init; }
}

public readonly struct DateTimeFilter
{
    public bool? IsNull { get; init; }
    public DateTime? Eq { get; init; }
    public DateTime? Ne { get; init; }
    public DateTime? Gt { get; init; }
    public DateTime? Gte { get; init; }
    public DateTime? Lt { get; init; }
    public DateTime? Lte { get; init; }
    public DateTime[]? In { get; init; }
    public DateTime[]? NotIn { get; init; }
}

public readonly struct DateOnlyFilter
{
    public bool? IsNull { get; init; }
    public DateOnly? Eq { get; init; }
    public DateOnly? Ne { get; init; }
    public DateOnly? Gt { get; init; }
    public DateOnly? Gte { get; init; }
    public DateOnly? Lt { get; init; }
    public DateOnly? Lte { get; init; }
    public DateOnly[]? In { get; init; }
    public DateOnly[]? NotIn { get; init; }
}

public readonly struct BoolFilter
{
    public bool? IsNull { get; init; }
    public bool? Eq { get; init; }
    public bool? Ne { get; init; }
}

public readonly struct GuidFilter
{
    public bool? IsNull { get; init; }
    public Guid? Eq { get; init; }
    public Guid? Ne { get; init; }
    public Guid[]? In { get; init; }
    public Guid[]? NotIn { get; init; }
}

public readonly struct EnumFilter<T> where T : struct, Enum
{
    public bool? IsNull { get; init; }
    public T? Eq { get; init; }
    public T? Ne { get; init; }
    public T? Gt { get; init; }
    public T? Gte { get; init; }
    public T? Lt { get; init; }
    public T? Lte { get; init; }
    public T[]? In { get; init; }
    public T[]? NotIn { get; init; }
}

public static class FilterHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string EscapeLike(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}