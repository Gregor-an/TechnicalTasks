using System.Security.Cryptography;
using Task2.LoginApi.Models;

namespace Task2.LoginApi.Services;

public static class PasswordHasher
{
    public static (string HashB64, string SaltB64, int Iterations) Hash(
        string password, int iterations = 100_000, int saltSize = 16, int hashSize = 32)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(saltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, hashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt), iterations);
    }

    public static bool Verify(User user, string password)
    {
        var salt = Convert.FromBase64String(user.SaltBase64);
        var expectedHash = Convert.FromBase64String(user.PasswordHashBase64);
        byte[] computed = Rfc2898DeriveBytes.Pbkdf2(password, salt, user.Iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(expectedHash, computed);
    }
}
