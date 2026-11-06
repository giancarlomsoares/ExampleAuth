using ExampleAuth.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExampleAuth.Server.Data;

public static class DbInitializer
{
    public const string AdministratorRole = "Administrator";
    public const string SupportRole = "Support";

    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<ExampleAuthDbContext>();
        await dbContext.Database.MigrateAsync();

        await EnsureAdministratorRoleAsync(provider);
        await EnsureSupportRoleAsync(provider);
        await EnsureBootstrapAdminAsync(provider);
    }

    private static async Task EnsureAdministratorRoleAsync(IServiceProvider provider)
    {
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(AdministratorRole))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(AdministratorRole));
        }
    }

    private static async Task EnsureSupportRoleAsync(IServiceProvider provider)
    {
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(SupportRole))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(SupportRole));
        }
    }

    private static async Task EnsureBootstrapAdminAsync(IServiceProvider provider)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var email = configuration["EXAMPLEAUTH_ADMIN_EMAIL"];
        var password = configuration["EXAMPLEAUTH_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Administrator",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, AdministratorRole);
        }
    }
}
