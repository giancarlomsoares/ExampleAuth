using System.Text;
using ExampleAuth.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace ExampleAuth.Server.Services;

public class EmailConfirmationService(
    UserManager<ApplicationUser> userManager,
    IEmailSender<ApplicationUser> emailSender,
    LinkGenerator linkGenerator,
    IOptions<ExampleAuthOptions> exampleAuthOptions,
    ILogger<EmailConfirmationService> logger)
{
    public async Task SendAsync(ApplicationUser user)
    {
        try
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            // Built from the configured public base URL, not the incoming request's Host header
            // (which AllowedHosts alone does not reliably reject) — see ExampleAuthOptions.
            var baseUri = new Uri(exampleAuthOptions.Value.PublicBaseUrl);
            var link = linkGenerator.GetUriByPage(
                page: "/Account/ConfirmEmail",
                handler: null,
                scheme: baseUri.Scheme,
                host: new HostString(baseUri.Authority),
                values: new { userId = user.Id, code = encodedToken });

            if (link is null)
            {
                logger.LogError("Could not build confirmation link for user {UserId}.", user.Id);
                return;
            }

            await emailSender.SendConfirmationLinkAsync(user, user.Email!, link);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to prepare or send confirmation email for user {UserId}.", user.Id);
        }
    }
}
