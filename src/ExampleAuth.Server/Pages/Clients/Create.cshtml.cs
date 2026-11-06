using System.Security.Cryptography;
using ExampleAuth.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;

namespace ExampleAuth.Server.Pages.Clients;

public class CreateModel(IOpenIddictApplicationManager applicationManager) : PageModel
{
    [BindProperty]
    public ClientKind Kind { get; set; } = ClientKind.Spa;

    [BindProperty]
    public string DisplayName { get; set; } = string.Empty;

    [BindProperty]
    public string RedirectUris { get; set; } = string.Empty;

    [BindProperty]
    public bool AllowUserManagement { get; set; }

    public bool Created { get; private set; }

    public string? CreatedClientId { get; private set; }

    public string? GeneratedSecret { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var clientId = Guid.NewGuid().ToString("N");

        if (Kind == ClientKind.Machine)
        {
            // Redirect URIs and the SPA flow don't apply here; any submitted values are ignored.
            var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            await applicationManager.CreateAsync(
                OpenIddictClientDescriptors.BuildMachine(clientId, secret, DisplayName, AllowUserManagement));
            GeneratedSecret = secret;
        }
        else
        {
            var redirectUris = (RedirectUris ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (redirectUris.Length == 0)
            {
                ErrorMessage = "Informe ao menos uma Redirect URI.";
                return Page();
            }

            if (Kind == ClientKind.Web)
            {
                var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
                await applicationManager.CreateAsync(
                    OpenIddictClientDescriptors.BuildWeb(clientId, secret, DisplayName, redirectUris));
                GeneratedSecret = secret;
            }
            else
            {
                await applicationManager.CreateAsync(
                    OpenIddictClientDescriptors.BuildSpa(clientId, DisplayName, redirectUris));
            }
        }

        Created = true;
        CreatedClientId = clientId;
        return Page();
    }
}
