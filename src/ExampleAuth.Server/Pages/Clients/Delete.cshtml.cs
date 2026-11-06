using ExampleAuth.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;

namespace ExampleAuth.Server.Pages.Clients;

public class DeleteModel(
    IOpenIddictApplicationManager applicationManager,
    ClientAccessService clientAccessService) : PageModel
{
    public string ClientId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ClientType { get; set; }
    public List<string> RedirectUris { get; } = [];
    public int LinkedUserCount { get; set; }

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
        if (application is not null)
        {
            await applicationManager.DeleteAsync(application);
        }

        return RedirectToPage("Index");
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
        LinkedUserCount = (await clientAccessService.ListForClientAsync(clientId)).Count;

        return true;
    }
}
