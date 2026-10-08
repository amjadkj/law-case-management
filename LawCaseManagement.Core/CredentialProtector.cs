using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LawCaseManagement.Core
{
    /// <summary>
    /// Provides encryption/decryption of sensitive strings (connection strings, credentials)
    /// using Windows DPAPI (Data Protection API). Encrypted data is bound to the current
    /// Windows user account — it cannot be decrypted by another user or on another machine.
    /// 
    /// This satisfies §6 of the technical requirements: "do not store SQL Server credentials
    /// in plain text in App.config."
    /// </summary>
    public static class CredentialProtector
    {
        /// <summary>
        /// Optional entropy to strengthen the DPAPI encryption beyond just user-scope.
        /// </summary>
        private static readonly byte[] AdditionalEntropy =
            Encoding.UTF8.GetBytes("LawCaseManagement.CredentialProtector.v1");

        /// <summary>
        /// Encrypts a plaintext string using DPAPI with CurrentUser scope.
        /// Returns a Base64-encoded string suitable for storage in config files.
        /// </summary>
        /// <param name="plainText">The plaintext connection string or credential.</param>
        /// <returns>Base64-encoded encrypted string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when plainText is null or empty.</exception>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                throw new ArgumentNullException(nameof(plainText));

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes,
                AdditionalEntropy,
                DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Decrypts a Base64-encoded DPAPI-encrypted string back to plaintext.
        /// Must be called by the same Windows user who encrypted the data.
        /// </summary>
        /// <param name="encryptedBase64">Base64-encoded encrypted string from <see cref="Encrypt"/>.</param>
        /// <returns>The original plaintext string.</returns>
        /// <exception cref="CryptographicException">Thrown if decryption fails (wrong user, tampered data).</exception>
        public static string Decrypt(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64))
                throw new ArgumentNullException(nameof(encryptedBase64));

            byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
            byte[] plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                AdditionalEntropy,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plainBytes);
        }

        /// <summary>
        /// Attempts to decrypt a connection string. If decryption fails (e.g., the string
        /// is not encrypted, or was encrypted by another user), returns the input unchanged
        /// and logs a warning. This allows graceful fallback during development.
        /// </summary>
        /// <param name="connectionStringOrEncrypted">Either a plaintext or DPAPI-encrypted connection string.</param>
        /// <param name="logger">Optional logger for warnings.</param>
        /// <returns>The decrypted connection string, or the original if decryption fails.</returns>
        public static string DecryptOrPassthrough(string connectionStringOrEncrypted, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(connectionStringOrEncrypted))
                return connectionStringOrEncrypted;

            // Quick heuristic: if it looks like a normal connection string (contains '=' and ';'),
            // it's probably not encrypted. Skip decryption attempt.
            if (connectionStringOrEncrypted.Contains(';') && connectionStringOrEncrypted.Contains('='))
                return connectionStringOrEncrypted;

            try
            {
                return Decrypt(connectionStringOrEncrypted);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex,
                    "Could not decrypt connection string — using as plaintext. " +
                    "If the string is encrypted, ensure it was encrypted by the current Windows user.");
                return connectionStringOrEncrypted;
            }
        }
    }
}
