using GymYanten.Models;
using Microsoft.AspNetCore.Identity;

namespace GymYanten.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // ── 1. Crear roles si no existen ──────────────────────────
            foreach (var rol in Roles.Todos)
            {
                if (!await roleManager.RoleExistsAsync(rol))
                    await roleManager.CreateAsync(new IdentityRole(rol));
            }

            // ── 2. Crear Administrador por defecto ────────────────────
            const string adminEmail = "admin@GymYanten.com";
            const string adminPass = "Admin@1234";   // ⚠️ Cambia esto en producción

            var admin = await userManager.FindByEmailAsync(adminEmail);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    Nombre = "Super",
                    Apellido = "Admin",
                    EmailConfirmed = true,
                    Activo = true
                };

                var result = await userManager.CreateAsync(admin, adminPass);

                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, Roles.Administrador);
            }
        }
    }
}
