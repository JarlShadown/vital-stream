using System.Security.Cryptography;
using System.Text;

namespace VitalStream.Shared.Security;

public static class HmacValidator
{
    /// <summary>
    /// Validates the HMAC-SHA256 signature of a request body.
    /// Expects the header value in the format: sha256=&lt;hex&gt;
    /// </summary>
    public static bool Validate(ReadOnlySpan<byte> body, string? signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(signatureHeader) ||
            !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        byte[] receivedHash;
        try
        {
            receivedHash = Convert.FromHexString(signatureHeader["sha256=".Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var computedHash = HMACSHA256.HashData(keyBytes, body);

        if (receivedHash.Length != computedHash.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(computedHash, receivedHash);
    }
}