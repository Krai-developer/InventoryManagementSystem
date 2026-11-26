using System;
using System.Security.Cryptography;

namespace InventoryManagementSystem.Security
{
    /// <summary>
    /// Secure salted password hashing using PBKDF2-HMAC-SHA256 with unique per-user salts
    /// and constant-time hash comparison.
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSize = 16;          // 128-bit unique salt
        private const int HashSize = 32;          // 256-bit derived key
        private const int Iterations = 100_000;   // OWASP-aligned PBKDF2-SHA256 work factor

        public static (string Hash, string Salt) HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be empty.", nameof(password));

            byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
        }

        public static bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            if (string.IsNullOrEmpty(password) ||
                string.IsNullOrWhiteSpace(storedHash) ||
                string.IsNullOrWhiteSpace(storedSalt))
            {
                return false;
            }

            try
            {
                byte[] saltBytes = Convert.FromBase64String(storedSalt);
                byte[] expectedHashBytes = Convert.FromBase64String(storedHash);

                byte[] actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    saltBytes,
                    Iterations,
                    HashAlgorithmName.SHA256,
                    expectedHashBytes.Length);

                return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
            }
            catch
            {
                return false;
            }
        }
    }
}
