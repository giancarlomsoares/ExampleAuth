using ExampleAuth.Server.Data;
using ExampleAuth.Server.Models;
using ExampleAuth.Server.Services;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ExampleAuth.Server.Controllers;

public class AuthorizationController(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictScopeManager scopeManager,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ClientAccessService clientAccessService,
    LoginAuditService loginAuditService) : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!result.Succeeded)
        {
            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                        Request.HasFormContentType ? [.. Request.Form] : [.. Request.Query])
                });
        }

        var user = await userManager.GetUserAsync(result.Principal)
            ?? throw new InvalidOperationException("The user details cannot be retrieved.");

        var application = await applicationManager.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("Details concerning the calling client application cannot be found.");

        var userId = await userManager.GetUserIdAsync(user);
        var applicationId = await applicationManager.GetIdAsync(application)
            ?? throw new InvalidOperationException("The application id cannot be retrieved.");

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();

        if (user.IsDisabled)
        {
            await loginAuditService.RecordAsync(
                LoginAuditEventType.ClientAuthorization,
                LoginAuditStatus.Failure,
                ipAddress,
                userAgent,
                user.Email ?? user.Id.ToString(),
                userId: user.Id,
                clientId: request.ClientId,
                failureReason: "account_disabled");

            return RedirectToPage("/Account/AccountDisabled");
        }

        if (!await userManager.IsInRoleAsync(user, DbInitializer.AdministratorRole)
            && !await clientAccessService.HasAccessAsync(user.Id, applicationId))
        {
            await loginAuditService.RecordAsync(
                LoginAuditEventType.ClientAuthorization,
                LoginAuditStatus.Failure,
                ipAddress,
                userAgent,
                user.Email ?? user.Id.ToString(),
                userId: user.Id,
                clientId: request.ClientId,
                failureReason: "client_access_denied");

            return RedirectToPage("/Account/ClientAccessDenied", new { client = request.ClientId });
        }

        var authorizations = await authorizationManager.FindAsync(
            subject: userId,
            client: applicationId,
            status: Statuses.Valid,
            type: AuthorizationTypes.Permanent,
            scopes: request.GetScopes()).ToListAsync();

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        principal.SetClaim(Claims.Subject, userId);
        principal.SetScopes(request.GetScopes());
        principal.SetResources(await scopeManager.ListResourcesAsync(principal.GetScopes()).ToListAsync());

        var authorization = authorizations.LastOrDefault();
        authorization ??= await authorizationManager.CreateAsync(
            principal: principal,
            subject: userId,
            client: applicationId,
            type: AuthorizationTypes.Permanent,
            scopes: principal.GetScopes());

        principal.SetAuthorizationId(await authorizationManager.GetIdAsync(authorization));

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim, principal));
        }

        await loginAuditService.RecordAsync(
            LoginAuditEventType.ClientAuthorization,
            LoginAuditStatus.Success,
            ipAddress,
            userAgent,
            user.Email ?? user.Id.ToString(),
            userId: user.Id,
            clientId: request.ClientId);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();

        if (request.IsClientCredentialsGrantType())
        {
            var application = await applicationManager.FindByClientIdAsync(request.ClientId!)
                ?? throw new InvalidOperationException("The calling client application cannot be found.");

            var identity = new ClaimsIdentity(
                authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                nameType: Claims.Name,
                roleType: Claims.Role);

            identity.SetClaim(Claims.Subject, await applicationManager.GetClientIdAsync(application));
            identity.SetClaim(Claims.Name, await applicationManager.GetDisplayNameAsync(application));
            identity.SetScopes(request.GetScopes());

            foreach (var claim in identity.Claims)
            {
                claim.SetDestinations(Destinations.AccessToken);
            }

            await loginAuditService.RecordAsync(
                LoginAuditEventType.ClientCredentialsToken,
                LoginAuditStatus.Success,
                ipAddress,
                userAgent,
                request.ClientId!,
                clientId: request.ClientId);

            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var eventType = request.IsRefreshTokenGrantType()
                ? LoginAuditEventType.TokenRefreshed
                : LoginAuditEventType.TokenIssued;

            var authenticateResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = authenticateResult.Principal
                ?? throw new InvalidOperationException("The user principal cannot be retrieved.");

            var user = await userManager.FindByIdAsync(principal.GetClaim(Claims.Subject)!);
            if (user is null || !await signInManager.CanSignInAsync(user) || user.IsDisabled)
            {
                await loginAuditService.RecordAsync(
                    eventType,
                    LoginAuditStatus.Failure,
                    ipAddress,
                    userAgent,
                    principal.GetClaim(Claims.Subject) ?? "unknown",
                    userId: user?.Id,
                    clientId: request.ClientId,
                    failureReason: "invalid_grant");

                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The token is no longer valid.",
                    }));
            }

            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(GetDestinations(claim, principal));
            }

            await loginAuditService.RecordAsync(
                eventType,
                LoginAuditStatus.Success,
                ipAddress,
                userAgent,
                user.Email ?? user.Id.ToString(),
                userId: user.Id,
                clientId: request.ClientId);

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new NotImplementedException("The specified grant type is not implemented.");
    }

    [Authorize]
    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await userManager.GetUserAsync(User);

        await loginAuditService.RecordAsync(
            LoginAuditEventType.Logout,
            LoginAuditStatus.Success,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.Headers.UserAgent.ToString(),
            user?.Email ?? "unknown",
            userId: user?.Id);

        await signInManager.SignOutAsync();

        return SignOut(
            authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
            properties: new AuthenticationProperties
            {
                RedirectUri = "/",
            });
    }

    private static IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
    {
        switch (claim.Type)
        {
            case Claims.Name or Claims.PreferredUsername:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Profile))
                {
                    yield return Destinations.IdentityToken;
                }
                yield break;

            case Claims.Email:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Email))
                {
                    yield return Destinations.IdentityToken;
                }
                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Roles))
                {
                    yield return Destinations.IdentityToken;
                }
                yield break;

            case "AspNet.Identity.SecurityStamp":
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}
