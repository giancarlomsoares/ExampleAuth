namespace ExampleAuth.Server.Models;

/// <summary>
/// Application-wide settings that are not secrets but still need to be explicit
/// configuration rather than derived from the incoming request. See <see cref="PublicBaseUrl"/>.
/// </summary>
public class ExampleAuthOptions
{
    /// <summary>
    /// The externally-reachable base URL of this IdP (e.g. "https://auth.example.com"), used to
    /// build absolute links embedded in emails (password reset, email confirmation) instead of
    /// trusting the incoming request's <c>Host</c> header. Relying on <c>Request.Host</c>/
    /// <c>LinkGenerator.GetUriByPage(HttpContext, ...)</c> is only as safe as <c>AllowedHosts</c>,
    /// and a forged <c>Host</c> header there previously let an attacker redirect a genuine password
    /// reset email to a domain of their choosing. Required outside <c>Development</c> — see the
    /// startup check in <c>Program.cs</c>.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Semicolon-separated hostnames this app accepts as the request's <c>Host</c> header, enforced by
    /// a custom middleware in <c>Program.cs</c> instead of ASP.NET Core's built-in "AllowedHosts"-config
    /// host filtering. That built-in middleware runs before anything this app adds to the pipeline and
    /// has no path-based exceptions — behind an ALB (e.g. ECS Express Mode) it also rejects the ALB's
    /// own health check probe, which hits the task's private IP directly rather than the real hostname,
    /// and there is no way to configure a custom Host header on an ALB target group health check. This
    /// property/middleware combo keeps the same strict enforcement for every other route while exempting
    /// <c>/health</c>. Required outside <c>Development</c> — see the startup check in <c>Program.cs</c>.
    /// </summary>
    public string AllowedHosts { get; set; } = string.Empty;
}
