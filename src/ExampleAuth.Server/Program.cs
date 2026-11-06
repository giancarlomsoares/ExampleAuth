using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using ExampleAuth.Server.Data;
using ExampleAuth.Server.Models;
using ExampleAuth.Server.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Scalar.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ExampleAuth.Server;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Database connection string not configured. Set it via the ConnectionStrings__Default environment variable or, in Development, via appsettings.Development.json.");

        builder.Services.AddDbContext<ExampleAuthDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.UseOpenIddict<Guid>();
            options.UseSnakeCaseNamingConvention();
        });

        builder.Services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;
                options.Tokens.PasswordResetTokenProvider = "PasswordResetTokenProvider";
            })
            .AddEntityFrameworkStores<ExampleAuthDbContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider<ApplicationUser>>("PasswordResetTokenProvider");

        builder.Services.Configure<PasswordResetTokenProviderOptions>(
            o => o.TokenLifespan = TimeSpan.FromHours(1));

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/connect/logout";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

        builder.Services
            .AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<ExampleAuthDbContext>()
                    .ReplaceDefaultEntities<Guid>();
            })
            .AddServer(options =>
            {
                options
                    .SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetUserInfoEndpointUris("connect/userinfo")
                    .SetEndSessionEndpointUris("connect/logout");

                options
                    .AllowAuthorizationCodeFlow()
                    .RequireProofKeyForCodeExchange()
                    .AllowClientCredentialsFlow()
                    .AllowRefreshTokenFlow();

                options.RegisterScopes(Scopes.Email, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess, "api", "users.manage");

                if (builder.Environment.IsDevelopment())
                {
                    options
                        .AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
                }
                else
                {
                    var certPath = builder.Configuration["EXAMPLEAUTH_SIGNING_CERT_PATH"]
                        ?? throw new InvalidOperationException("EXAMPLEAUTH_SIGNING_CERT_PATH must be configured in non-development environments.");

                    var certPassword = builder.Configuration["EXAMPLEAUTH_SIGNING_CERT_PASSWORD"]
                        ?? throw new InvalidOperationException("EXAMPLEAUTH_SIGNING_CERT_PASSWORD must be configured in non-development environments.");

                    var certificate = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword);
                    options.AddSigningCertificate(certificate);
                    options.AddEncryptionCertificate(certificate);
                }

                options
                    .UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        builder.Services.AddScoped<ClientAccessService>();
        builder.Services.AddScoped<LoginAuditService>();
        builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
        builder.Services.AddTransient<IEmailSender<ApplicationUser>, SmtpEmailSender>();
        builder.Services.AddScoped<EmailConfirmationService>();
        builder.Services.AddScoped<PasswordResetService>();

        builder.Services.Configure<ExampleAuthOptions>(builder.Configuration.GetSection("ExampleAuth"));

        // PublicBaseUrl is what emailed links (password reset, email confirmation) are built from,
        // instead of the incoming request's Host header — see ExampleAuthOptions for why. Development gets
        // a working default from appsettings.Development.json; every other environment must set it
        // explicitly via configuration, or startup fails closed rather than silently trusting Host.
        if (string.IsNullOrWhiteSpace(builder.Configuration["ExampleAuth:PublicBaseUrl"]) && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("ExampleAuth:PublicBaseUrl must be configured outside Development (the externally-reachable base URL of this deployment, e.g. https://auth.example.com) — it is used to build links in emails instead of trusting the incoming request's Host header.");
        }

        // Enforced by our own middleware below, not the built-in "AllowedHosts" config key — see
        // ExampleAuthOptions.AllowedHosts for why (the built-in one can't exempt the ALB health check path).
        if (string.IsNullOrWhiteSpace(builder.Configuration["ExampleAuth:AllowedHosts"]) && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("ExampleAuth:AllowedHosts must be configured outside Development (semicolon-separated hostnames this deployment is reached at, e.g. auth.example.com).");
        }

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("Administrator", policy => policy.RequireRole(DbInitializer.AdministratorRole))
            .AddPolicy("Staff", policy => policy.RequireRole(DbInitializer.AdministratorRole, DbInitializer.SupportRole))
            .AddPolicy("ClientUserManagement", policy => policy
                .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                .RequireAssertion(context => context.User.HasScope("users.manage")));
        builder.Services.AddRateLimiter(options =>
        {
            // Partitioned per client IP (not one global counter — see the finding this fixes) and only
            // ever charges POST: [EnableRateLimiting("PasswordReset")] on ForgotPasswordModel is a
            // class-level attribute because Razor Pages maps OnGet/OnPost of the same page to a single
            // endpoint, so a handler-method-level [DisableRateLimiting] on OnGet is not observed —
            // excluding GET has to happen in the partition selector itself, via GetNoLimiter.
            options.AddPolicy("PasswordReset", httpContext =>
            {
                if (HttpMethods.IsGet(httpContext.Request.Method))
                {
                    return RateLimitPartition.GetNoLimiter("password-reset-get");
                }

                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0,
                });
            });
        });
        builder.Services.AddControllers();
        builder.Services.AddRazorPages(options =>
        {
            // The admin panel (Pages/Index, Pages/Users/*) is open to both Administrator and Support (the "Staff" policy);
            // Pages/Clients/* additionally requires the "Administrator" policy, and since folder-level authorize
            // conventions combine (AND), that narrows /Clients/* to Administrator only. Punch anonymous holes only
            // for the pages that must work before login.
            options.Conventions.AuthorizeFolder("/", "Staff");
            options.Conventions.AuthorizeFolder("/Clients", "Administrator");
            options.Conventions.AllowAnonymousToPage("/Account/Login");
            options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
            options.Conventions.AllowAnonymousToPage("/Account/ClientAccessDenied");
            options.Conventions.AllowAnonymousToPage("/Account/AccountDisabled");
            options.Conventions.AllowAnonymousToPage("/Account/ConfirmEmail");
            options.Conventions.AllowAnonymousToPage("/Account/ForgotPassword");
            options.Conventions.AllowAnonymousToPage("/Account/ResetPassword");
        });
        builder.Services.AddOpenApi();

        var app = builder.Build();

        await DbInitializer.RunAsync(app.Services);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        // Only the ALB's security group can reach this task (see ECS task security group),
        // so trusting every forwarded header here is safe — there's no untrusted hop in between.
        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };
        forwardedHeadersOptions.KnownIPNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedHeadersOptions);

        // Replaces the built-in "AllowedHosts"-config host filtering (never configured — see
        // ExampleAuthOptions.AllowedHosts): that middleware runs before anything else in this pipeline with
        // no path exceptions, so behind an ALB it also 400s the ALB's own health check probe, which hits
        // the task's private IP rather than our hostname and can't be given a custom Host header.
        var allowedHosts = (app.Configuration["ExampleAuth:AllowedHosts"] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/health") || allowedHosts.Contains(context.Request.Host.Host))
            {
                await next();
                return;
            }

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        });

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapRazorPages();
        app.MapControllers();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        await app.RunAsync();
    }
}
