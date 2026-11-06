using ExampleAuth.Server.Data;
using ExampleAuth.Server.Models;
using ExampleAuth.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;

namespace ExampleAuth.Server.Pages.Users;

public class DetailsModel(
    UserManager<ApplicationUser> userManager,
    ClientAccessService clientAccessService,
    IOpenIddictApplicationManager applicationManager,
    EmailConfirmationService emailConfirmationService) : PageModel
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string[] Roles { get; set; } = [];
    public bool IsDisabled { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedByEmail { get; set; }
    public List<GrantRow> Grants { get; } = [];
    public List<ClientOption> AllClients { get; } = [];
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await LoadAsync(id))
        {
            return RedirectToPage("Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostResendConfirmationAsync(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return RedirectToPage("Index");
        }

        if (!user.EmailConfirmed)
        {
            await emailConfirmationService.SendAsync(user);
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostGrantAsync(Guid id, string clientId)
    {
        if (!User.IsInRole(DbInitializer.AdministratorRole))
        {
            return Forbid();
        }

        var grantedByUserId = Guid.Parse(userManager.GetUserId(User)!);
        var granted = await clientAccessService.GrantAsync(id, clientId, grantedByUserId);
        if (!granted)
        {
            ErrorMessage = "Client não encontrado.";
            await LoadAsync(id);
            return Page();
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid id, string clientId)
    {
        if (!User.IsInRole(DbInitializer.AdministratorRole))
        {
            return Forbid();
        }

        var revokedByUserId = Guid.Parse(userManager.GetUserId(User)!);
        await clientAccessService.RevokeAsync(
            id,
            clientId,
            revokedByUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.Headers.UserAgent.ToString());
        return RedirectToPage(new { id });
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
        EmailConfirmed = user.EmailConfirmed;
        Roles = [.. (await userManager.GetRolesAsync(user)).Order()];
        CreatedAtUtc = user.CreatedAtUtc;
        CreatedByEmail = user.CreatedByUserId.HasValue
            ? (await userManager.FindByIdAsync(user.CreatedByUserId.Value.ToString()))?.Email
            : null;

        Grants.Clear();
        foreach (var grant in await clientAccessService.ListForUserAsync(id))
        {
            var application = await applicationManager.FindByIdAsync(grant.ApplicationId.ToString());
            var clientId = application is null ? null : await applicationManager.GetClientIdAsync(application);
            var displayName = application is null ? null : await applicationManager.GetDisplayNameAsync(application);
            var grantedBy = await userManager.FindByIdAsync(grant.GrantedByUserId.ToString());
            Grants.Add(new GrantRow(clientId, displayName, grantedBy?.Email, grant.GrantedAtUtc));
        }

        AllClients.Clear();
        await foreach (var application in applicationManager.ListAsync())
        {
            AllClients.Add(new ClientOption(
                (await applicationManager.GetClientIdAsync(application))!,
                await applicationManager.GetDisplayNameAsync(application)));
        }

        return true;
    }
}

public record GrantRow(string? ClientId, string? DisplayName, string? GrantedByEmail, DateTimeOffset GrantedAtUtc);

public record ClientOption(string ClientId, string? DisplayName);
