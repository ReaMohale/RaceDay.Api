using System.Security.Cryptography;

namespace RaceDay.Api.Security;

public interface IPasswordService
{
    /// <summary>Hashes a password with a fresh random salt. The result is safe to store in the database.</summary>
    string Hash(string password);

    /// <summary>Checks a password against a hash created by <see cref="Hash"/>.</summary>
    bool Verify(string password, string storedHash);
}

/// <summary>
/// PBKDF2 (HMAC-SHA256) with a random salt per password. The stored text looks like
/// <c>v1.{iterations}.{salt}.{hash}</c>, so the cost can be raised later without breaking old hashes.
/// </summary>
public class PasswordService : IPasswordService
{
    private const string Version = "v1";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 600_000;       // OWASP recommendation for PBKDF2-HMAC-SHA256
    private const int MaxAcceptedIterations = 5_000_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Version}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return false;

        var parts = storedHash.Split('.');
        if (parts.Length != 4 || parts[0] != Version
            || !int.TryParse(parts[1], out var iterations) || iterations < 1 || iterations > MaxAcceptedIterations)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expected.Length == 0)
            return false;

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
