// SECURITY FIX #1 (cont.) — Adaptador para integrar PasswordHasherService con ASP.NET Core Identity.
// Identity llama a IPasswordHasher<TUser> internamente; este wrapper delega al servicio seguro.
// Soporta migración transparente: hashes legacy de Identity se re-hashean al primer login exitoso.

using GymYanten.Models;
using Microsoft.AspNetCore.Identity;

namespace GymYanten.Security
{
    /// <summary>
    /// Reemplaza el <see cref="IPasswordHasher{TUser}"/> default de Identity.
    /// Registrar en Program.cs vía:
    ///   builder.Services.AddScoped&lt;IPasswordHasher&lt;ApplicationUser&gt;, CustomIdentityPasswordHasher&gt;();
    /// ANTES de .AddIdentity(…).
    /// </summary>
    public class CustomIdentityPasswordHasher : IPasswordHasher<ApplicationUser>
    {
        private readonly PasswordHasherService _hasher;

        public CustomIdentityPasswordHasher(PasswordHasherService hasher)
            => _hasher = hasher;

        // ── HashPassword ─────────────────────────────────────────────────────
        public string HashPassword(ApplicationUser user, string password)
        {
            // SECURITY FIX: usa PBKDF2-HMAC-SHA256 100k iteraciones
            return _hasher.HashPassword(password);
        }

        // ── VerifyHashedPassword ─────────────────────────────────────────────
        public PasswordVerificationResult VerifyHashedPassword(
            ApplicationUser user,
            string hashedPassword,
            string providedPassword)
        {
            // SECURITY FIX: detectar hashes legacy de Identity (empiezan con 0x01 en Base64 → "AQ==")
            // y hashes V2 del formato propio (marcador 0x02).
            if (IsLegacyIdentityHash(hashedPassword))
            {
                // Verificar con el hasher default de Identity para no romper cuentas existentes.
                // Identity marcará SuccessRehashNeeded → SignInManager re-hashea automáticamente.
                var legacyHasher = new PasswordHasher<ApplicationUser>();
                var legacyResult = legacyHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);

                // SECURITY FIX: si el password legacy era correcto, pedir rehash inmediato
                return legacyResult == PasswordVerificationResult.Success
                    ? PasswordVerificationResult.SuccessRehashNeeded
                    : PasswordVerificationResult.Failed;
            }

            // Hash propio: verificar normalmente
            return _hasher.VerifyPassword(hashedPassword, providedPassword)
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.Failed;
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        /// <summary>
        /// Detecta hashes generados por el PasswordHasher nativo de Identity.
        /// Identity V3 (default en .NET 8) almacena hashes que, decodificados,
        /// tienen el primer byte = 0x01.
        /// </summary>
        private static bool IsLegacyIdentityHash(string hash)
        {
            try
            {
                var bytes = Convert.FromBase64String(hash);
                return bytes.Length > 0 && bytes[0] == 0x01;
            }
            catch { return false; }
        }
    }
}
