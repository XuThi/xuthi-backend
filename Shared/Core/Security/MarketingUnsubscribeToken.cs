using System.Security.Cryptography;
using System.Text;

namespace Core.Security;

public static class MarketingUnsubscribeToken
{
    public static string CreateToken(string email, string signingKey, DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        if (string.IsNullOrWhiteSpace(signingKey))
            throw new ArgumentException("Signing key is required.", nameof(signingKey));

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var payloadRaw = $"{normalizedEmail}\n{expiresAtUtc.ToUnixTimeSeconds()}";
        var payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadRaw));
        var signaturePart = ComputeSignature(payloadPart, signingKey);

        return $"{payloadPart}.{signaturePart}";
    }

    public static bool TryValidateToken(
        string? token,
        string signingKey,
        out string email)
    {
        email = string.Empty;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(signingKey))
            return false;

        var parts = token.Split('.', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        var payloadPart = parts[0];
        var signaturePart = parts[1];
        var expectedSignature = ComputeSignature(payloadPart, signingKey);

        if (!FixedTimeEquals(signaturePart, expectedSignature))
            return false;

        if (!TryBase64UrlDecode(payloadPart, out var payloadBytes))
            return false;

        var payloadRaw = Encoding.UTF8.GetString(payloadBytes);

        if (!TryParsePayload(payloadRaw, out var parsedEmail, out var expiresAtUnix))
            return false;

        var minUnix = DateTimeOffset.UnixEpoch.ToUnixTimeSeconds();
        var maxUnix = DateTimeOffset.MaxValue.ToUnixTimeSeconds();
        if (expiresAtUnix <= minUnix || expiresAtUnix > maxUnix)
            return false;

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix);
        if (expiresAt <= DateTimeOffset.UtcNow)
            return false;

        email = parsedEmail;
        return true;
    }

    private static bool TryParsePayload(string payloadRaw, out string email, out long expiresAtUnix)
    {
        email = string.Empty;
        expiresAtUnix = 0;

        var separatorIndex = payloadRaw.LastIndexOf('\n');
        if (separatorIndex > 0 && separatorIndex < payloadRaw.Length - 1)
        {
            var parsedEmail = payloadRaw[..separatorIndex].Trim().ToLowerInvariant();
            var expiresPart = payloadRaw[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(parsedEmail) || !long.TryParse(expiresPart, out expiresAtUnix))
                return false;

            email = parsedEmail;
            return true;
        }

        // Backward compatibility for previously issued JSON payload tokens.
        const string emailPrefix = "{\"Email\":\"";
        const string expiresMarker = "\",\"ExpiresAtUnix\":";

        if (!payloadRaw.StartsWith(emailPrefix, StringComparison.Ordinal) || !payloadRaw.EndsWith('}'))
            return false;

        var markerIndex = payloadRaw.IndexOf(expiresMarker, StringComparison.Ordinal);
        if (markerIndex <= emailPrefix.Length)
            return false;

        var legacyEmail = payloadRaw[emailPrefix.Length..markerIndex].Trim().ToLowerInvariant();
        var legacyExpires = payloadRaw[(markerIndex + expiresMarker.Length)..^1].Trim();

        if (string.IsNullOrWhiteSpace(legacyEmail) || !long.TryParse(legacyExpires, out expiresAtUnix))
            return false;

        email = legacyEmail;
        return true;
    }

    private static string ComputeSignature(string payloadPart, string signingKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        var dataBytes = Encoding.UTF8.GetBytes(payloadPart);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Base64UrlEncode(hash);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryBase64UrlDecode(string input, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var normalized = input
            .Replace('-', '+')
            .Replace('_', '/');

        var paddingLength = (4 - normalized.Length % 4) % 4;
        normalized = normalized + new string('=', paddingLength);

        var buffer = new byte[(normalized.Length / 4) * 3];
        if (!Convert.TryFromBase64String(normalized, buffer, out var bytesWritten))
            return false;

        bytes = buffer.AsSpan(0, bytesWritten).ToArray();
        return true;
    }
}