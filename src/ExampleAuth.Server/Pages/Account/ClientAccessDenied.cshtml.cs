using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;

namespace ExampleAuth.Server.Pages.Account;

public class ClientAccessDeniedModel(IOpenIddictApplicationManager applicationManager) : PageModel
{
    public string ClientDisplayName { get; set; } = string.Empty;

    public async Task OnGetAsync(string? client)
    {
        var application = client is null ? null : await applicationManager.FindByClientIdAsync(client);
        ClientDisplayName = application is not null
            ? (await applicationManager.GetDisplayNameAsync(application)) ?? client!
            : client ?? "desconhecido";
    }
}
