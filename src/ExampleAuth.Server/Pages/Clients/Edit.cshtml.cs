using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ExampleAuth.Server.Pages.Clients;

public class EditModel(IOpenIddictApplicationManager applicationManager) : PageModel
{
    public string ClientId { get; set; } = string.Empty;

    [BindProperty]
    public string DisplayName { get; set; } = string.Empty;

    [BindProperty]
    public string RedirectUris { get; set; } = string.Empty;

    [BindProperty]
    public bool AllowUserManagement { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string clientId)
    {
        if (!await LoadAsync(clientId))
        {
            return RedirectToPage("Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string clientId)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId);
        if (application is null)
        {
            return RedirectToPage("Index");
        }

        var redirectUris = (RedirectUris ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (redirectUris.Length == 0)
        {
            ErrorMessage = "Informe ao menos uma Redirect URI.";
            ClientId = clientId;
            return Page();
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, application);

        descriptor.DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? clientId : DisplayName;

        descriptor.RedirectUris.Clear();
        foreach (var redirectUri in redirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(redirectUri));
        }

        var usersManageScope = Permissions.Prefixes.Scope + "users.manage";
        if (AllowUserManagement)
        {
            descriptor.Permissions.Add(usersManageScope);
        }
        else
        {
            descriptor.Permissions.Remove(usersManageScope);
        }

        await applicationManager.UpdateAsync(application, descriptor);

        return RedirectToPage("Details", new { clientId });
    }

    private async Task<bool> LoadAsync(string clientId)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId);
        if (application is null)
        {
            return false;
        }

        ClientId = clientId;
        DisplayName = await applicationManager.GetDisplayNameAsync(application) ?? clientId;
        RedirectUris = string.Join('\n', await applicationManager.GetRedirectUrisAsync(application));

        var permissions = await applicationManager.GetPermissionsAsync(application);
        AllowUserManagement = permissions.Contains(Permissions.Prefixes.Scope + "users.manage");

        return true;
    }
}
