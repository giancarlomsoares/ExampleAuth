using ExampleAuth.Server.Data;
using ExampleAuth.Server.Models;
using ExampleAuth.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;

namespace ExampleAuth.Server.Pages.Users;

public class EditModel(
    UserManager<ApplicationUser> userManager,
    IOpenIddictAuthorizationManager authorizationManager,
    LoginAuditService loginAuditService) : PageModel
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string? DisplayName { get; set; }

    [BindProperty]
    public string SelectedRole { get; set; } = "None";

    [BindProperty]
    public bool IsDisabled { get; set; }

    public Guid CurrentUserId { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await LoadAsync(id))
        {
            return RedirectToPage("Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return RedirectToPage("Index");
        }

        var currentUserId = Guid.Parse(userManager.GetUserId(User)!);
        var isAdministrator = User.IsInRole(DbInitializer.AdministratorRole);
        var isSelf = id == currentUserId;

        user.DisplayName = DisplayName;

        var wasDisabled = user.IsDisabled;
        if (isAdministrator && !isSelf)
        {
            if (IsDisabled && !wasDisabled)
            {
                user.DisabledAtUtc = DateTimeOffset.UtcNow;
                user.DisabledByUserId = currentUserId;
            }
            else if (!IsDisabled && wasDisabled)
            {
                user.DisabledAtUtc = null;
                user.DisabledByUserId = null;
            }
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            ErrorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            await LoadAsync(id);
            return Page();
        }

        if (isAdministrator && !isSelf)
        {
            var currentRoles = await userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Intersect(new[] { DbInitializer.AdministratorRole, DbInitializer.SupportRole }).ToArray();
            if (rolesToRemove.Length > 0)
            {
                await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            }

            if (SelectedRole is DbInitializer.AdministratorRole or DbInitializer.SupportRole)
            {
                await userManager.AddToRoleAsync(user, SelectedRole);
            }

            if (user.IsDisabled && !wasDisabled)
            {
                await authorizationManager.RevokeBySubjectAsync(user.Id.ToString());

                await loginAuditService.RecordAsync(
                    LoginAuditEventType.UserDisabled,
                    LoginAuditStatus.Success,
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    Request.Headers.UserAgent.ToString(),
                    user.Email ?? user.Id.ToString(),
                    userId: user.Id);
            }
        }

        return RedirectToPage("Details", new { id });
    }

    private async Task<bool> LoadAsync(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return false;
        }

        Id = user.Id;
        Email = user.Email!;
        DisplayName = user.DisplayName;
        IsDisabled = user.IsDisabled;
        CurrentUserId = Guid.Parse(userManager.GetUserId(User)!);

        var roles = await userManager.GetRolesAsync(user);
        SelectedRole = roles.Contains(DbInitializer.AdministratorRole)
            ? DbInitializer.AdministratorRole
            : roles.Contains(DbInitializer.SupportRole)
                ? DbInitializer.SupportRole
                : "None";

        return true;
    }
}
