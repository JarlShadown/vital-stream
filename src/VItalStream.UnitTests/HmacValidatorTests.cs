using System.Security.Cryptography;
using System.Text;
using VitalStream.Shared.Security;

namespace VItalStream.UnitTests;

public class HmacValidatorTests
{
    private static string BuildHeader(byte[] body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, body);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Test]
    public void ValidSignature_ReturnsTrue()
    {
        var body = "{ \"heartRate\": 72 }"u8.ToArray();
        var secret = "super-secret";
        var header = BuildHeader(body, secret);

        Assert.That(HmacValidator.Validate(body, header, secret), Is.True);
    }

    [Test]
    public void TamperedBody_ReturnsFalse()
    {
        var original = "{ \"heartRate\": 72 }"u8.ToArray();
        var tampered = "{ \"heartRate\": 999 }"u8.ToArray();
        var header = BuildHeader(original, "super-secret");

        Assert.That(HmacValidator.Validate(tampered, header, "super-secret"), Is.False);
    }

    [Test]
    public void WrongSecret_ReturnsFalse()
    {
        var body = "{ \"heartRate\": 72 }"u8.ToArray();
        var header = BuildHeader(body, "correct-secret");

        Assert.That(HmacValidator.Validate(body, header, "wrong-secret"), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    public void MissingHeader_ReturnsFalse(string? header)
    {
        var body = "test"u8.ToArray();

        Assert.That(HmacValidator.Validate(body, header, "secret"), Is.False);
    }

    [Test]
    public void HeaderWithoutPrefix_ReturnsFalse()
    {
        var body = "test"u8.ToArray();
        var key = Encoding.UTF8.GetBytes("secret");
        var hash = Convert.ToHexString(HMACSHA256.HashData(key, body));

        // No "sha256=" prefix
        Assert.That(HmacValidator.Validate(body, hash, "secret"), Is.False);
    }

    [Test]
    public void HeaderWithInvalidHex_ReturnsFalse()
    {
        Assert.That(HmacValidator.Validate("test"u8, "sha256=not-valid-hex!!", "secret"), Is.False);
    }

    [Test]
    public void UpperCaseHexInHeader_ReturnsTrue()
    {
        var body = "{ \"spO2\": 98 }"u8.ToArray();
        var secret = "device-secret";
        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, body);
        var header = "sha256=" + Convert.ToHexString(hash).ToUpperInvariant();

        Assert.That(HmacValidator.Validate(body, header, secret), Is.True);
    }

    [Test]
    public void EmptyBody_ValidSignature_ReturnsTrue()
    {
        var body = Array.Empty<byte>();
        var header = BuildHeader(body, "secret");

        Assert.That(HmacValidator.Validate(body, header, "secret"), Is.True);
    }
}