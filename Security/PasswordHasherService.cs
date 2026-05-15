// SECURITY FIX #1 — Upgrade password hashing: PBKDF2 100k iterations + HMAC-SHA256
// Reemplaza el hasher default de Identity (PBKDF2 10k iter, SHA-1 internamente).
// Referencia: NIST SP 800-132, OWASP Password Storage Cheat Sheet 2024.

using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace GymYanten.Security
{
    /// <summary>
    /// Servicio de hash de contraseñas con PBKDF2-HMAC-SHA256 a 100 000 iteraciones.
    /// Formato del hash almacenado (Base64):
    ///   [1 byte versión=0x02] [16 bytes salt] [32 bytes subkey]
    /// Total: 49 bytes → ~68 chars en Base64.
    /// </summary>
    public class PasswordHasherService
    {
        // SECURITY FIX: 100 000 iteraciones (10× el default de Identity 8.x)
        private const int Iterations    = 100_000;
        private const int SaltSize      = 16;   // 128 bits
        private const int KeySize       = 32;   // 256 bits
        private const byte FormatMarker = 0x02; // Versión interna de este hasher

        // ── Hash ─────────────────────────────────────────────────────────────
        public string HashPassword(string password)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            // SECURITY FIX: salt criptográficamente aleatorio por contraseña
            var salt = RandomNumberGenerator.GetBytes(SaltSize);

            var subkey = KeyDerivation.Pbkdf2(
                password:   password,
                salt:       salt,
                prf:        KeyDerivationPrf.HMACSHA256,  // SECURITY FIX: SHA-256 en lugar de SHA-1
                iterationCount: Iterations,
                numBytesRequested: KeySize);

            // Ensamblar: [marker | salt | subkey]
            var output = new byte[1 + SaltSize + KeySize];
            output[0] = FormatMarker;
            Buffer.BlockCopy(salt,   0, output, 1,            SaltSize);
            Buffer.BlockCopy(subkey, 0, output, 1 + SaltSize, KeySize);

            return Convert.ToBase64String(output);
        }

        // ── Verificar ────────────────────────────────────────────────────────
        /// <returns>
        ///   true  → contraseña correcta  <br/>
        ///   false → contraseña incorrecta o hash con formato desconocido
        /// </returns>
        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hashedPassword);
            ArgumentException.ThrowIfNullOrWhiteSpace(providedPassword);

            byte[] decoded;
            try { decoded = Convert.FromBase64String(hashedPassword); }
            catch { return false; } // Base64 inválido

            // Validar longitud mínima y marcador de versión
            if (decoded.Length != 1 + SaltSize + KeySize || decoded[0] != FormatMarker)
                return false;

            var salt = new byte[SaltSize];
            Buffer.BlockCopy(decoded, 1, salt, 0, SaltSize);

            var storedSubkey = new byte[KeySize];
            Buffer.BlockCopy(decoded, 1 + SaltSize, storedSubkey, 0, KeySize);

            var providedSubkey = KeyDerivation.Pbkdf2(
                password:          providedPassword,
                salt:              salt,
                prf:               KeyDerivationPrf.HMACSHA256,
                iterationCount:    Iterations,
                numBytesRequested: KeySize);

            // SECURITY FIX: comparación de tiempo constante para evitar timing attacks
            return CryptographicOperations.FixedTimeEquals(storedSubkey, providedSubkey);
        }
    }
}
