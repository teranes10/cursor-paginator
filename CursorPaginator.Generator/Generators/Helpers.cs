using System.Security.Cryptography;

namespace CursorPaginator.Generator.Generators;

internal class Helpers
{
    public static string GetBinaryReadMethod(string baseType, bool enumAsString = false)
    {
        if (enumAsString) return "r.ReadString()";

        return baseType switch
        {
            "string" => "r.ReadString()",
            "int" or "System.Int32" => "r.ReadInt32()",
            "long" or "System.Int64" => "r.ReadInt64()",
            "decimal" or "System.Decimal" => "r.ReadDecimal()",
            "float" or "System.Single" => "r.ReadSingle()",
            "double" or "System.Double" => "r.ReadDouble()",
            "bool" or "System.Boolean" => "r.ReadBoolean()",
            "System.DateTime" => "DateTime.FromBinary(r.ReadInt64())",
            "System.DateOnly" => "DateOnly.FromDayNumber(r.ReadInt32())",
            "System.Guid" => "new Guid(r.ReadBytes(16))",
            _ => $"({baseType})r.ReadInt32()" // Enum types
        };
    }

    public static string GetBinaryWriteMethod(string baseType, bool enumAsString = false)
    {
        if (enumAsString) return "w.Write(v.ToString()!)";

        return baseType switch
        {
            "string" => "w.Write((string)v)",
            "int" or "System.Int32" => "w.Write((int)v)",
            "long" or "System.Int64" => "w.Write((long)v)",
            "decimal" or "System.Decimal" => "w.Write((decimal)v)",
            "float" or "System.Single" => "w.Write((float)v)",
            "double" or "System.Double" => "w.Write((double)v)",
            "bool" or "System.Boolean" => "w.Write((bool)v)",
            "System.DateTime" => "w.Write(((DateTime)v).ToBinary())",
            "System.DateOnly" => "w.Write(((DateOnly)v).DayNumber)",
            "System.Guid" => "w.Write(((Guid)v).ToByteArray())",
            _ => "w.Write((int)v)" // Enum types
        };
    }

    public static string GetArrayWriteMethod(string baseType, bool enumAsString = false)
    {
        if (enumAsString)
            return @"(w, v) => { var arr = (" + baseType + @"[])v; w.Write(arr.Length); foreach (var e in arr) w.Write(e.ToString()); }";

        var elementType = baseType switch
        {
            "string" => "string",
            "int" or "System.Int32" => "int",
            "long" or "System.Int64" => "long",
            "decimal" or "System.Decimal" => "decimal",
            "float" or "System.Single" => "float",
            "double" or "System.Double" => "double",
            "bool" or "System.Boolean" => "bool",
            "System.DateTime" => "DateTime",
            "System.DateOnly" => "DateOnly",
            "System.Guid" => "Guid",
            _ => baseType // Enum types use full name
        };

        if (baseType == "System.DateTime")
            return @"(w, v) => { var arr = (" + elementType + @"[])v; w.Write(arr.Length); foreach (var dt in arr) w.Write(dt.ToBinary()); }";
        else if (baseType == "System.DateOnly")
            return @"(w, v) => { var arr = (" + elementType + @"[])v; w.Write(arr.Length); foreach (var dt in arr) w.Write(dt.DayNumber); }";
        else if (baseType == "System.Guid")
            return @"(w, v) => { var arr = (" + elementType + @"[])v; w.Write(arr.Length); foreach (var g in arr) w.Write(g.ToByteArray()); }";
        else if (baseType is "string" or "int" or "System.Int32" or "long" or "System.Int64" or "decimal" or "System.Decimal" or "float" or "System.Single" or "double" or "System.Double" or "bool" or "System.Boolean")
            return @"(w, v) => { var arr = (" + elementType + @"[])v; w.Write(arr.Length); foreach (var e in arr) w.Write(e); }";
        else // Enum types
            return @"(w, v) => { var arr = (" + elementType + @"[])v; w.Write(arr.Length); foreach (var e in arr) w.Write((int)e); }";
    }

    public static int StableHashSha256(string s)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return BitConverter.ToInt32(hashBytes, 0);
    }

    public static string QuoteIdentifier(string identifier, string quote)
    {
        if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(quote))
            return identifier;

        if (identifier == "*" || identifier.StartsWith(quote))
            return identifier;

        var parts = identifier.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part != "*" && !part.StartsWith(quote) && !string.IsNullOrEmpty(part))
            {
                parts[i] = $"{quote}{part}{quote}";
            }
        }
        return string.Join(".", parts);
    }
}
