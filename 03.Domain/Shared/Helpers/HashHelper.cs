using System.Security.Cryptography;
using System.Text;

namespace expense_tracker_backend.Domain.Shared.Helpers;

/// <summary>
/// Hashing and encoding helper methods
/// </summary>
public static class HashHelper
{
    /// <summary>
    /// Generate SHA256 hash
    /// </summary>
    public static string Sha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Generate secure random string (for backup codes, tokens, etc.)
    /// </summary>
    public static string GenerateSecureRandomString(int length = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes)[..length];
    }

    /// <summary>
    /// Generate backup codes for MFA
    /// </summary>
    public static List<string> GenerateBackupCodes(int count = 6, int length = 8)
    {
        var codes = new List<string>();
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        
        for (int i = 0; i < count; i++)
        {
            var code = new StringBuilder();
            for (int j = 0; j < length; j++)
            {
                code.Append(chars[RandomNumberGenerator.GetInt32(chars.Length)]);
            }
            codes.Add($"BACKUP-{code}");
        }
        
        return codes;
    }

    /// <summary>
    /// Encode to Base64
    /// </summary>
    public static string ToBase64(string input) 
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(input));

    /// <summary>
    /// Decode from Base64
    /// </summary>
    public static string FromBase64(string base64) 
        => Encoding.UTF8.GetString(Convert.FromBase64String(base64));
}
