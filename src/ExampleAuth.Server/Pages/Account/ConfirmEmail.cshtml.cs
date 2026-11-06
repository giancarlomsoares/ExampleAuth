using System.Text;
using ExampleAuth.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace ExampleAuth.Server.Pages.Account;

public class ConfirmEmailModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public ConfirmEmailStatus Status { get; set; }

    public async Task OnGetAsync(Guid? userId, string? code)
    {
        if (userId is null || code is null)
        {
            Status = ConfirmEmailStatus.InvalidLink;
            return;
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            Status = ConfirmEmailStatus.InvalidLink;
            return;
        }

        if (user.EmailConfirmed)
        {
            Status = ConfirmEmailStatus.AlreadyConfirmed;
            return;
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            Status = ConfirmEmailStatus.InvalidLink;
            return;
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        Status = result.Succeeded ? ConfirmEmailStatus.Confirmed : ConfirmEmailStatus.InvalidLink;
    }
}

public enum ConfirmEmailStatus
{
    Confirmed,
    AlreadyConfirmed,
    InvalidLink,
}
