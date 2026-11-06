# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ExampleAuth is a corporate Identity Provider (IdP): a self-hosted OAuth 2.1 / OpenID
Connect authorization server built with OpenIddict, meant to centralize user
authentication for multiple corporate web apps.

## Commands

All commands run from `src/ExampleAuth.Server/` unless noted.

```bash
# build
dotnet build

# run (Development reads appsettings.Development.json — first time: cp appsettings.Development.example.json appsettings.Development.json)
dotnet run --launch-profile https   # https://localhost:7061
dotnet run --launch-profile http    # http://localhost:5213

# migrations (requires the dotnet-ef global tool: dotnet tool install --global dotnet-ef --version 10.0.10)
dotnet ef migrations add <Name> -o Data/Migrations
dotnet ef database update
```

There are no automated tests in this repo yet.

VS Code users can just use "Run and Debug" → **"ExampleAuth.Server (https)"** (defined
in `.vscode/launch.json`/`tasks.json`); it builds first and opens Scalar
(`/scalar/v1`) automatically once Kestrel is listening.

### SDK gotcha

This repo targets `net10.0` on a very recent SDK (10.0.110) where `dotnet build`/
`restore` fails with `NETSDK1226: Prune Package data not found` unless
`AllowMissingPrunePackageData` is set — already set in `ExampleAuth.Server.csproj`, so
this should be transparent, but if you ever see that error on a different machine/SDK
version, that property is why.

### Frontend styling (Tailwind CSS)

The admin UI uses Tailwind CSS, compiled by the **standalone Tailwind CLI** (no Node/npm
required — a single downloaded binary). One-time setup per machine:

```bash
mkdir -p ~/.local/bin
curl -sLo ~/.local/bin/tailwindcss https://github.com/tailwindlabs/tailwindcss/releases/download/v3.4.17/tailwindcss-linux-x64
chmod +x ~/.local/bin/tailwindcss
```

(Substitute `tailwindcss-macos-x64`, `tailwindcss-macos-arm64`, or
`tailwindcss-windows-x64.exe` from the same
[v3.4.17 release](https://github.com/tailwindlabs/tailwindcss/releases/tag/v3.4.17) on other
platforms.)

`wwwroot/css/site.css` is the **compiled, minified** stylesheet and is committed to git —
`dotnet run`/`dotnet build` never need Tailwind installed. Only rebuild it when you change
Tailwind classes in a `.cshtml` file or edit `wwwroot/css/input.css`/`tailwind.config.js`,
then commit the regenerated `site.css` alongside your markup change:

```bash
cd src/ExampleAuth.Server
~/.local/bin/tailwindcss -i wwwroot/css/input.css -o wwwroot/css/site.css --minify
```

For active styling work, run it in watch mode instead of re-running it by hand:

```bash
~/.local/bin/tailwindcss -i wwwroot/css/input.css -o wwwroot/css/site.css --watch
```

## Configuration (nothing environment-specific in appsettings.json)

`appsettings.json` only holds logging. The database connection string, SMTP settings,
`ExampleAuth:*` settings and all bootstrap secrets are read from configuration, never
hardcoded — in a deployment (Azure/AWS app settings, container env vars) they are
environment variables; in `Development` they come from a local file (see below):

- `ConnectionStrings__Default` (env var, standard ASP.NET Core convention) — required
  everywhere.
- `Smtp__Host` / `__Port` / `__Username` / `__Password` / `__FromAddress` (plus optional
  `__FromName`, `__UseStartTls`) — bound to `SmtpOptions`; nothing validates them at
  startup, so a missing value only surfaces when an email is actually sent.
- `ExampleAuth__PublicBaseUrl` / `ExampleAuth__AllowedHosts` — required outside
  `Development` (`Program.cs` throws at startup otherwise). `AllowedHosts` feeds a host-filter
  middleware that runs in **every** environment, so an empty value in `Development` makes
  every request except `/health` return `400`.
- In `Development`, `appsettings.Development.json` is each developer's own file:
  gitignored (`appsettings.*.json` in `.gitignore`) and never committed. Seed it by copying
  the versioned template `appsettings.Development.example.json` (neutral values, matches
  `docker-compose.yml`). `Data/ExampleAuthDbContextFactory.cs` (used by
  `dotnet ef`) mirrors the same layering (`appsettings.json` → `appsettings.{env}.json` →
  env vars), so `dotnet ef` commands and `dotnet run` always see the same config.
- `EXAMPLEAUTH_ADMIN_EMAIL` / `EXAMPLEAUTH_ADMIN_PASSWORD` — optional; if set, `DbInitializer`
  bootstraps that user with the `Administrator` role on startup (idempotent: only
  runs if no user with that email exists yet). This is the only way to create the
  *first* admin — there's no self-registration endpoint. `DbInitializer` also seeds
  the `Support` role itself (same idempotent pattern), but never a bootstrap `Support`
  user — every promotion to `Administrator` or `Support` after the first admin exists
  happens through `Pages/Users/Edit.cshtml.cs` (`OnPostSetRoleAsync`), restricted to
  existing Administrators (see Architecture).
- `EXAMPLEAUTH_SIGNING_CERT_PATH` / `_PASSWORD` — required outside `Development` (a PFX
  used to sign/encrypt tokens). In `Development`, OpenIddict generates its own
  development certificate automatically.

## Architecture

**Two identity concepts share one `ExampleAuthDbContext`:** ASP.NET Core Identity (users,
roles, password hashes — the `AspNetUsers`/`AspNetRoles` tables) and OpenIddict's own
entities (`UseOpenIddict<Guid>()` in `Program.cs`, wired into the *same* `DbContext`).
Any `DbContextOptionsBuilder` used to build/query this context — including the
design-time factory (`Data/ExampleAuthDbContextFactory.cs`) — must call both
`UseNpgsql(...)` **and** `UseOpenIddict<Guid>()`, or migrations will silently miss half
the schema (this happened once; see the `InitialCreate` migration history if debugging
schema drift). The `<Guid>` matters too: `AddOpenIddict().AddCore(...)` in `Program.cs`
also calls `.ReplaceDefaultEntities<Guid>()`, so all four OpenIddict tables
(`Applications`/`Authorizations`/`Scopes`/`Tokens`) use `uuid` keys like the rest of the
schema instead of OpenIddict's `string` default — both places need to agree on the key
type, or the model won't build.

**OpenIddict manager APIs always surface ids as `string`,** regardless of the
configured `TKey`: `IOpenIddictApplicationManager.GetIdAsync(application)` returns a
`string` even though the underlying `OpenIddictApplications.Id` column is `uuid`. Code
that needs to compare/store that id against a `Guid` column (see
`Services/ClientAccessService.cs`) has to `Guid.Parse(...)` it explicitly — this is by
design in OpenIddict, to keep the manager interfaces agnostic to the key type.

**Auth surfaces, by mechanism:**
- `/connect/*` (Controllers/AuthorizationController.cs, UserinfoController.cs) — the
  actual OAuth/OIDC protocol endpoints (authorize, token, userinfo, logout). These
  follow OpenIddict's controller-based passthrough pattern (`EnableXxxPassthrough()`
  in `Program.cs` + a matching controller action) rather than fully automatic
  handling, because the app needs custom logic (e.g., resolving the ASP.NET Core
  Identity user behind an authorization code).
- `/Account/Login`, `/`, `/Clients/*`, `/Users/*` (Razor Pages) — the human-facing side.
  Two staff roles exist, `Administrator` and `Support` (`DbInitializer.AdministratorRole`
  / `SupportRole`, both seeded on startup — see Configuration above). Protected via Razor
  Pages **conventions** in `Program.cs`, not per-page `[Authorize]` attributes:
  `AuthorizeFolder("/", "Staff")` (policy = `RequireRole(AdministratorRole, SupportRole)`,
  i.e. either role) opens the whole panel, and `AuthorizeFolder("/Clients", "Administrator")`
  layers a stricter requirement on top. Folder-level authorize conventions **combine**
  (AND) rather than the more specific one overriding the broader one, so `/Clients/*` ends
  up satisfying both `Staff` and `Administrator` — which in practice means Administrator-only,
  since `Administrator` is the only role common to both. `AllowAnonymousToPage` carves out
  `/Account/Login`, `/Account/AccessDenied` and `/Account/ClientAccessDenied` from all of
  this — "AllowAnonymous" here really means "skip the policy for this page", not "skip
  authentication". A new page under `Pages/` (outside `/Clients`) gets the `Staff` policy
  for free; a new Administrator-only page needs its own stricter `AuthorizeFolder` (or
  `[Authorize(Roles = "Administrator")]`), since today only `/Clients` has one.

  **Administrator vs. Support, concretely:** both roles can edit a user's display name
  (`Pages/Users/Edit.cshtml.cs`, `OnPostUpdateNameAsync` — no extra check beyond the
  `Staff` policy). Only `Administrator` can reach `/Clients/*` at all, grant/revoke a
  `ClientAccessGrant` (from either `Pages/Clients/Details.cshtml` or
  `Pages/Users/Edit.cshtml` — `OnPostGrantAsync`/`OnPostRevokeAsync` explicitly re-check
  `User.IsInRole(AdministratorRole)` and `Forbid()` otherwise, since the view already
  hides those controls for Support but a forged POST could still hit the handler), or
  change another user's role (`OnPostSetRoleAsync`, same explicit check; the view hides
  the role selector entirely for Support). Support viewing `Pages/Users/Edit.cshtml` still
  sees the current role and the client-grant list, just without controls to change them.
  The one hard rule inside `OnPostSetRoleAsync`: an `Administrator` can never change their
  **own** role (compares the target `id` to `userManager.GetUserId(User)`) — demoting or
  fully removing the role of any *other* Administrator or Support user has no such
  restriction. `Pages/Users/Create.cshtml.cs` has the same shape: the role selector and the
  client-grant checkboxes are both wrapped in `User.IsInRole(AdministratorRole)` in the view
  *and* in `OnPostAsync` (Support-submitted `SelectedRole`/`ClientIds` are silently ignored,
  same forged-POST defense as above) — a user created by Support always starts with no role
  and no client grants.

  **Disabling a user is a separate axis from roles and client grants, and deliberately
  not built on Identity's own lockout:** `ApplicationUser.DisabledAtUtc`/`DisabledByUserId`
  (mirroring `ClientAccessGrant`'s `RevokedAtUtc`/`RevokedByUserId` audit pattern) back an
  `IsDisabled` computed property, rather than reusing `LockoutEnd` (already wired up via
  `options.Lockout.*` in `Program.cs` for the automatic 5-failed-attempt lockout, which
  expires on its own after 5 minutes). Overloading that column would mix two state
  machines with different lifecycles, and is already ambiguous with the "Locked" badge
  in `Pages/Users/Index.cshtml.cs`, which is exclusively about the failed-attempt
  lockout. `IsDisabled` is enforced independently in the three places a session or token
  could be created or renewed: `Pages/Account/Login.cshtml.cs` (checked before calling
  `PasswordSignInAsync`, which knows nothing about this field), `AuthorizationController.
  Authorize()` (redirects to `/Account/AccountDisabled`, an anonymous-allowed page like
  `ClientAccessDenied`), and `AuthorizationController.Exchange()` (folded into the same
  `!await signInManager.CanSignInAsync(user)` check that already gates the
  `authorization_code`/`refresh_token` branch — `CanSignInAsync` alone doesn't consult
  `LockoutEnd` either, so this check was already necessary there regardless of which
  field backs "disabled"). Disabling also calls
  `authorizationManager.RevokeBySubjectAsync(userId)` to immediately kill every refresh
  token the user holds across every client — the cross-client counterpart to
  `ClientAccessService.RevokeAsync`'s per-client `FindAsync(...)` + `TryRevokeAsync` loop.
  An already-open Identity cookie session in the admin panel is *not* torn down
  immediately — same accepted gap as revoking a `ClientAccessGrant` (see below): the
  block takes effect on the next login or token-refresh attempt, not mid-session. Only
  `Administrator` can toggle this (`Pages/Users/Edit.cshtml.cs`,
  `OnPostToggleDisabledAsync`), and — same self-protection shape as `OnPostSetRoleAsync`
  — can never disable or re-enable their *own* account, only another user's (including
  another Administrator's).
- `/api/users` (`Controllers/ClientUsersController.cs`) — an M2M (machine-to-machine)
  API that lets an OAuth client manage its own users, authenticated with a
  `client_credentials` bearer token carrying the `users.manage` scope (opt-in per
  client, checked via the `ClientUserManagement` policy — the OpenIddict validation
  scheme, not the Identity cookie; see the authentication scheme note below). Actions:
  `POST /api/users` (create + auto-grant), `PATCH /api/users/{id}` (edit
  `displayName`), `POST /api/users/{id}/reset-password`, and
  `DELETE /api/users/{id}/access?actingUserId={guid}` (revoke the caller's own grant).
  The calling client is always resolved from the token's `sub` claim, never from a
  request parameter, and every action on an existing `{id}` first checks
  `ClientAccessService.HasAccessAsync` — a client can only touch users already linked
  to it (`404`, not `403`, if not). Administrator **and Support** accounts are always
  `404` through this API regardless of any grant, to close a privilege-escalation path
  (a client should never be able to reset a staff member's password). There is no admin JSON API
  anymore — `/Clients/*`, `/Users/*` Razor Pages are the only way admins manage
  clients/users/grants; the previous `Endpoints/AdminEndpoints.cs` (`/api/clients`,
  Administrator-cookie-gated `/api/users`) was deleted, since it never had a real
  consumer.

- `/Account/ForgotPassword`, `/Account/ResetPassword` (Razor Pages, anonymous) —
  self-service password reset. `ForgotPasswordModel.OnPostAsync` always shows the same
  "check your email" response regardless of whether the address matches a user, to avoid
  turning the flow into an account-existence oracle; `PasswordResetService.SendAsync`
  enforces the same silently-do-nothing behavior server-side for a nonexistent user or one
  with `IsDisabled == true` (the same eligibility gate used everywhere else in this
  codebase — **not** `EmailConfirmed`, which this flow doesn't consult). The reset token
  comes from a dedicated `PasswordResetTokenProvider<TUser>` (`Services/
  PasswordResetTokenProvider.cs`) with a 1-hour lifespan, registered under
  `options.Tokens.PasswordResetTokenProvider = "PasswordResetTokenProvider"` in
  `Program.cs` — deliberately not the default token provider that backs email
  confirmation (`DataProtectorTokenProvider<TUser>`, ~24h lifespan via `AddDefaultTokenProviders()`).
  **Gotcha that was hit and fixed during this feature's QA:** the obvious-looking way to
  give this flow its own lifespan — a *named* `builder.Services.Configure<
  DataProtectionTokenProviderOptions>("PasswordResetTokenProvider", o => o.TokenLifespan =
  ...)` registration — is silently never applied, because `DataProtectorTokenProvider<TUser>`
  only ever reads the *unnamed* `IOptions<DataProtectionTokenProviderOptions>.Value`; it
  never calls `IOptionsMonitor/Snapshot.Get(name)`, so a named `Configure<T>(name, ...)`
  registration has no effect on it. The fix is `PasswordResetTokenProviderOptions` — a
  subclass of `DataProtectionTokenProviderOptions` with its own dedicated (unnamed)
  `IOptions<PasswordResetTokenProviderOptions>`, consumed by a matching
  `PasswordResetTokenProvider<TUser>` subclass. Apply this same subclass pattern, not the
  named-provider one, for any future per-purpose token provider. On success,
  `ResetPasswordModel.OnPostAsync` calls `authorizationManager.RevokeBySubjectAsync(userId)`
  — same cross-client refresh-token revocation used when disabling a user — so a leaked
  old password can't be combined with an already-issued refresh token after the reset.

`Pages/Clients/Create.cshtml.cs` creates OAuth clients through the shared
`Services/OpenIddictClientDescriptors.cs`. The form has a radio for the client kind
(`ClientKind`): `Spa` (`BuildSpa` — public, no secret, Authorization Code + PKCE +
refresh, redirect URIs required, no `client_credentials`), `Web` (`BuildWeb` — same as
`Spa` but confidential with a generated secret, for apps with their own backend) and `Machine` (`BuildMachine` —
confidential with a generated secret, `client_credentials` only, no redirect URIs or
authorize/end_session endpoints, optional `users.manage` checkbox shown only for this
kind). There is no bootstrap client in `DbInitializer`; clients are created manually
in the admin UI. When changing what a registered client is allowed to do, this is the
one place to edit.

**Per-client access control (licensing) is deny-by-default:** a `ClientAccessGrant`
row (`Models/ClientAccessGrant.cs`) is the only thing that lets a non-`Administrator`
user complete `/connect/authorize` for a given OAuth client — there's no "open to
everyone" client. `Services/ClientAccessService.cs` is the single place that grants,
revokes, lists, and checks these grants; `AuthorizationController.Authorize()` calls
`HasAccessAsync` right after resolving `application`/`applicationId` and before
`SignIn()` — deny it there (redirect to `/Account/ClientAccessDenied`) and no
`OpenIddictAuthorization`/code is ever created. Revoking a grant also revokes any
matching `OpenIddictAuthorization`s, so an existing refresh token stops working on its
next use (an already-issued short-lived access token is not retroactively invalidated —
an accepted trade-off, since it expires on its own shortly). `Administrator` users bypass the check
entirely; `Support` does not — a `Support` user needs a `ClientAccessGrant` like any
regular user to complete `/connect/authorize` for a client. The `client_credentials`
grant handled in `AuthorizationController.Exchange()` skips the check too — there's no
human `subject` to check there.
Admins manage grants from either direction — `Pages/Clients/Details.cshtml`
(who has access to this client) or `Pages/Users/Edit.cshtml` (which clients this
user can access) — both backed by the same service, so there's no divergent logic to
keep in sync. `ClientAccessGrant.ApplicationId` is a plain `Guid` column with **no** EF
navigation to OpenIddict's application entity; the actual `FOREIGN KEY ... ON DELETE
CASCADE` to `OpenIddictApplications.Id` is hand-added directly in the
`AddClientAccessGrant` migration (see the comment there) precisely so `dotnet ef
migrations add` never tries to touch it.

**Authentication scheme note:** the Identity cookie (`IdentityConstants.ApplicationScheme`,
configured via `ConfigureApplicationCookie`) is the default scheme, used by the Razor
Pages. `/api/users` (`ClientUsersController`) is the exception: its `ClientUserManagement`
policy explicitly requires `OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme`
(a bearer token) instead, since a client calling it has no Identity cookie to send — it's
a `client_credentials` caller, not a browser session. Forbidden requests against the
Razor Pages get a 302 redirect to `AccessDeniedPath` (standard cookie-auth behavior);
`/api/users` returns a JSON 401/403 instead, since bearer-token auth doesn't redirect.

**Razor Pages form gotcha:** `Pages/_ViewImports.cshtml` enables Tag Helpers
(`@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`) project-wide, but a bare
`<form method="post">` with no `asp-*` attribute does **not** get the antiforgery
token injected automatically — the Form Tag Helper only activates when at least one
`asp-*` attribute (e.g. `asp-page-handler`) is present. Forms without one (see
`Pages/Account/Login.cshtml`, `Pages/Clients/Create.cshtml`) add
`@Html.AntiForgeryToken()` explicitly instead.

**OpenIddict `SignIn()` requires an explicit `sub` claim:**
`signInManager.CreateUserPrincipalAsync(user)` (ASP.NET Core Identity) builds a
principal keyed on `ClaimTypes.NameIdentifier`, not the `Claims.Subject` ("sub") claim
that OpenIddict's `ValidateSignInDemand` handler requires — without it, every
`SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)` throws
`InvalidOperationException: ... mandatory subject claim was missing`, which surfaces as
a 500 on `/connect/authorize`. `AuthorizationController.Authorize()` sets it explicitly
right after creating the principal (`principal.SetClaim(Claims.Subject, userId)`) —
keep that line if you ever touch this method; without it the entire Authorization Code
Flow breaks for every client, not just new ones.

**Emailed links are built from configuration, never from the request's `Host` header:**
`PasswordResetService` and `EmailConfirmationService` used to call
`LinkGenerator.GetUriByPage(httpContext, ...)`, which builds the absolute URL from the
incoming request's `Host` header — with `AllowedHosts` permissive (previously `"*"`),
a forged `Host` header on `POST /Account/ForgotPassword` produced a genuine,
correctly-branded reset email pointing at an attacker-controlled domain, handing over a
live reset token on click (confirmed exploitable against Administrator accounts). Fixed
two ways, and both matter: `AllowedHosts` is now an explicit list (`"localhost;127.0.0.1"`
in the versioned `appsettings.Development.example.json`, an environment variable in a
deployment) instead of `"*"` — **a real deployment must set this to its actual public hostname via
configuration**, or requests carrying any other `Host` header are rejected outright before
they even reach a page handler. On top of that, both services now read
`ExampleAuth:PublicBaseUrl` (`Models/ExampleAuthOptions.cs`, bound the same `Configure<T>(section)`
way as `SmtpOptions`) and call the `LinkGenerator.GetUriByPage(page, handler, scheme, host,
values, ...)` overload that takes an explicit `scheme`/`HostString` and no `HttpContext` at
all, so the link is correct regardless of what a caller claims as `Host`. This setting is
required outside `Development` (`Program.cs` throws at startup if it's unset there, the
same fail-closed pattern as `EXAMPLEAUTH_SIGNING_CERT_PATH`); the versioned
`appsettings.Development.example.json` defaults it to `https://localhost:7061`. Since neither service needs `HttpContext` for link
generation anymore, `EmailConfirmationService.SendAsync` dropped that parameter entirely —
`PasswordResetService.SendAsync` keeps it, but only to read the caller's IP/user-agent for
the audit record described below.

**Password-reset rate limiting is partitioned per IP, and GETs are free:** the original
`AddFixedWindowLimiter("PasswordReset", ...)` was a single global counter — 5 requests from
*anyone*, including page loads, exhausted the limiter for *everyone* for 15 minutes, a
self-inflicted DoS on the recovery path. `Program.cs` now uses `AddPolicy("PasswordReset",
...)` with a partition-key selector keyed on `httpContext.Connection.RemoteIpAddress`
(`RateLimitPartition.GetFixedWindowLimiter`), so each IP gets its own 5-per-15-minute
budget (`QueueLimit = 0`, same as before). `[EnableRateLimiting("PasswordReset")]` on
`ForgotPasswordModel` is still a **class-level** attribute, deliberately: Razor Pages maps
`OnGet`/`OnPost` of the same page to a single endpoint, so a handler-method-level
`[DisableRateLimiting]` on just `OnGet` is not observed by the endpoint's rate-limiting
metadata — this was verified, not assumed. Instead, the policy's partition selector itself
returns `RateLimitPartition.GetNoLimiter(...)` for any GET, so page loads never consume the
per-IP budget; only `OnPost` charges it. No `UseForwardedHeaders()` is configured, so
`RemoteIpAddress` is the direct TCP peer — behind a reverse proxy this would need revisiting
to read the real client IP from `X-Forwarded-For` instead.

**Password-reset request/completion are audited like login:** `LoginAuditService`
(`Services/LoginAuditService.cs`) — the same one `Login.cshtml.cs` uses — now also records
`LoginAuditEventType.PasswordResetRequested` (from `PasswordResetService.SendAsync`, only on
the branch where a real, enabled user actually gets the email — logging on the
nonexistent-user/disabled-account branches would itself be a distinguishable side channel,
defeating the anti-enumeration behavior described above) and
`LoginAuditEventType.PasswordResetCompleted` (from `ResetPasswordModel.OnPostAsync`, only on
the success branch, after `RevokeBySubjectAsync`). Both are plain additions to the
`LoginAuditEventType` enum in `Models/LoginAudit.cs` — no migration needed, since
`ExampleAuthDbContext` maps `EventType`/`Status` with `HasConversion<string>()`, so the column
is `text`, not an integer.

**Bootstrapping is idempotent and additive, not a reset:** `DbInitializer.RunAsync`
runs on every startup, applies pending migrations, and only creates the bootstrap
admin/client if they don't already exist by email/client_id. Changing
`EXAMPLEAUTH_ADMIN_EMAIL` to a new value creates an *additional* admin rather than
renaming the old one.
