using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ExampleAuth.Server.Services;

public enum ClientKind
{
    // SPA / mobile: public client, Authorization Code + PKCE (+ refresh), no secret.
    Spa,
    // Web app with its own backend: confidential client, Authorization Code + PKCE (+ refresh), with secret.
    Web,
    // Server-to-server: confidential client, client_credentials only, no redirect URIs.
    Machine,
}

// Shared client registration shape (permissions, grant types, PKCE requirement)
// used by the admin UI (Pages/Clients/Create.cshtml.cs).
public static class OpenIddictClientDescriptors
{
    public static OpenIddictApplicationDescriptor BuildSpa(
        string clientId,
        string? displayName,
        IEnumerable<string> redirectUris) =>
        BuildAuthorizationCode(clientId, null, displayName, redirectUris);

    public static OpenIddictApplicationDescriptor BuildWeb(
        string clientId,
        string clientSecret,
        string? displayName,
        IEnumerable<string> redirectUris) =>
        BuildAuthorizationCode(clientId, clientSecret, displayName, redirectUris);

    private static OpenIddictApplicationDescriptor BuildAuthorizationCode(
        string clientId,
        string? clientSecret,
        string? displayName,
        IEnumerable<string> redirectUris)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientType = clientSecret is null ? ClientTypes.Public : ClientTypes.Confidential,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? clientId : displayName,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + "api",
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange,
            },
        };

        foreach (var redirectUri in redirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(redirectUri));
        }

        return descriptor;
    }

    public static OpenIddictApplicationDescriptor BuildMachine(
        string clientId,
        string clientSecret,
        string? displayName,
        bool allowUserManagement)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientType = ClientTypes.Confidential,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? clientId : displayName,
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.Prefixes.Scope + "api",
            },
        };

        if (allowUserManagement)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + "users.manage");
        }

        return descriptor;
    }
}
