using System.Security.Cryptography;

namespace CursorPaginator.Core;

public sealed class CursorSecurity
{
    private static byte[] _key;

    static CursorSecurity()
    {
        _key = RandomNumberGenerator.GetBytes(32);
    }

    public static void Init(string base64Key)
    {
        _key = Convert.FromBase64String(base64Key);
        if (_key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes", nameof(base64Key));
    }

    public static string SignCursor(byte[] data)
    {
        using var hmac = new HMACSHA256(_key);
        var signature = hmac.ComputeHash(data);

        var result = new byte[data.Length + signature.Length];
        Buffer.BlockCopy(data, 0, result, 0, data.Length);
        Buffer.BlockCopy(signature, 0, result, data.Length, signature.Length);

        return Convert.ToBase64String(result);
    }

    public static bool VerifyAndExtract(string cursor, out byte[] data)
    {
        data = [];

        try
        {
            if (string.IsNullOrWhiteSpace(cursor))
                return false;

            var combined = Convert.FromBase64String(cursor);
            if (combined.Length < 32) return false;

            var dataLength = combined.Length - 32;
            data = new byte[dataLength];
            var signature = new byte[32];

            Buffer.BlockCopy(combined, 0, data, 0, dataLength);
            Buffer.BlockCopy(combined, dataLength, signature, 0, 32);

            using var hmac = new HMACSHA256(_key);
            var computedSignature = hmac.ComputeHash(data);

            return CryptographicOperations.FixedTimeEquals(signature, computedSignature);
        }
        catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
        {
            return false;
        }
    }
}
