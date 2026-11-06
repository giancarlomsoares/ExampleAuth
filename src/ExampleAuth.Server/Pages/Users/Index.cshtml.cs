using ExampleAuth.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExampleAuth.Server.Pages.Users;

public class IndexModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public List<UserRow> Users { get; } = [];

    public async Task OnGetAsync()
    {
        var all = userManager.Users.OrderBy(u => u.Email).ToList();
        var emailById = all.ToDictionary(u => u.Id, u => u.Email);

        foreach (var user in all)
        {
            var roles = await userManager.GetRolesAsync(user);
            var createdByEmail = user.CreatedByUserId.HasValue
                ? emailById.GetValueOrDefault(user.CreatedByUserId.Value)
                : null;
            Users.Add(new UserRow(
                user.Id,
                user.Email!,
                user.DisplayName,
                [.. roles.Order()],
                user.EmailConfirmed,
                user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow,
                user.CreatedAtUtc,
                createdByEmail));
        }
    }
}

public record UserRow(Guid Id, string Email, string? DisplayName, string[] Roles, bool EmailConfirmed, bool Locked, DateTimeOffset CreatedAtUtc, string? CreatedByEmail);
