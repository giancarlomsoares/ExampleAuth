using System.Text;
using ExampleAuth.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace ExampleAuth.Server.Services;

public class PasswordResetService(
    UserManager<ApplicationUser> userManager,
    IEmailSender<ApplicationUser> emailSender,
    LinkGenerator linkGenerator,
    LoginAuditService loginAuditService,
    IOptions<ExampleAuthOptions> exampleAuthOptions,
    ILogger<PasswordResetService> logger)
{
    public async Task SendAsync(string email, HttpContext httpContext)
    {
        try
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null || user.IsDisabled)
            {
                return;
            }

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            // Built from the configured public base URL, not the incoming request's Host header
            // (which AllowedHosts alone does not reliably reject) — see ExampleAuthOptions.
            var baseUri = new Uri(exampleAuthOptions.Value.PublicBaseUrl);
            var link = linkGenerator.GetUriByPage(
                page: "/Account/ResetPassword",
                handler: null,
                scheme: baseUri.Scheme,
                host: new HostString(baseUri.Authority),
                values: new { userId = user.Id, code = encodedToken });

            if (link is null)
            {
                logger.LogError("Could not build password reset link for user {UserId}.", user.Id);
                return;
            }

            await emailSender.SendPasswordResetLinkAsync(user, user.Email!, link);

            await loginAuditService.RecordAsync(
                LoginAuditEventType.PasswordResetRequested,
                LoginAuditStatus.Success,
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                httpContext.Request.Headers.UserAgent.ToString(),
                email,
                userId: user.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to prepare or send password reset email for {Email}.", email);
        }
    }
}
