using ExampleAuth.Server.Models;
using ExampleAuth.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;

namespace ExampleAuth.Server.Pages.Clients;

public class DetailsModel(
    IOpenIddictApplicationManager applicationManager,
    ClientAccessService clientAccessService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public string ClientId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ClientType { get; set; }
    public List<string> RedirectUris { get; } = [];
    public List<GrantRow> Grants { get; } = [];
    public List<UserOption> AllUsers { get; } = [];
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string clientId)
    {
        if (!await LoadAsync(clientId))
        {
            return RedirectToPage("Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGrantAsync(string clientId, Guid userId)
    {
        var grantedByUserId = Guid.Parse(userManager.GetUserId(User)!);
        var granted = await clientAccessService.GrantAsync(userId, clientId, grantedByUserId);
        if (!granted)
        {
            ErrorMessage = "Client não encontrado.";
            await LoadAsync(clientId);
            return Page();
        }

        return RedirectToPage(new { clientId });
    }

    public async Task<IActionResult> OnPostRevokeAsync(string clientId, Guid userId)
    {
        var revokedByUserId = Guid.Parse(userManager.GetUserId(User)!);
        await clientAccessService.RevokeAsync(
            userId,
            clientId,
            revokedByUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.Headers.UserAgent.ToString());
        return RedirectToPage(new { clientId });
    }

    private async Task<bool> LoadAsync(string clientId)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId);
        if (application is null)
        {
            return false;
        }

        ClientId = clientId;
        DisplayName = await applicationManager.GetDisplayNameAsync(application);
        ClientType = await applicationManager.GetClientTypeAsync(application);
        RedirectUris.Clear();
        RedirectUris.AddRange(await applicationManager.GetRedirectUrisAsync(application));

        Grants.Clear();
        foreach (var grant in await clientAccessService.ListForClientAsync(clientId))
        {
            var user = await userManager.FindByIdAsync(grant.UserId.ToString());
            var grantedBy = await userManager.FindByIdAsync(grant.GrantedByUserId.ToString());
            Grants.Add(new GrantRow(grant.UserId, user?.Email, grantedBy?.Email, grant.GrantedAtUtc));
        }

        AllUsers.Clear();
        foreach (var user in userManager.Users.OrderBy(u => u.Email))
        {
            AllUsers.Add(new UserOption(user.Id, user.Email!));
        }

        return true;
    }
}

public record GrantRow(Guid UserId, string? Email, string? GrantedByEmail, DateTimeOffset GrantedAtUtc);

public record UserOption(Guid Id, string Email);
