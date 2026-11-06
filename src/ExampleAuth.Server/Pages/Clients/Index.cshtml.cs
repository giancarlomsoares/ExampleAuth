using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;

namespace ExampleAuth.Server.Pages.Clients;

public class IndexModel(IOpenIddictApplicationManager applicationManager) : PageModel
{
    public List<ClientRow> Clients { get; } = [];

    public async Task OnGetAsync()
    {
        await foreach (var application in applicationManager.ListAsync())
        {
            Clients.Add(new ClientRow(
                (await applicationManager.GetClientIdAsync(application))!,
                await applicationManager.GetDisplayNameAsync(application),
                (await applicationManager.GetClientTypeAsync(application))!,
                [.. await applicationManager.GetRedirectUrisAsync(application)]));
        }
    }
}

public record ClientRow(string ClientId, string? DisplayName, string ClientType, string[] RedirectUris);
