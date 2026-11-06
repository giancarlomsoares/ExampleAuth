using System.Text;
using ExampleAuth.Server.Models;
using ExampleAuth.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;

namespace ExampleAuth.Server.Pages.Account;

public class ResetPasswordModel(
    UserManager<ApplicationUser> userManager,
    IOpenIddictAuthorizationManager authorizationManager,
    LoginAuditService loginAuditService) : PageModel
{
    [BindProperty]
    public Guid UserId { get; set; }

    [BindProperty]
    public string Code { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    public bool InvalidLink { get; private set; }
    public bool Succeeded { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void OnGet(Guid userId, string code)
    {
        UserId = userId;
        Code = code;

        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            InvalidLink = true;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "As senhas não coincidem.";
            return Page();
        }

        var user = await userManager.FindByIdAsync(UserId.ToString());

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code));
        }
        catch (FormatException)
        {
            InvalidLink = true;
            return Page();
        }

        if (user is null || user.IsDisabled)
        {
            InvalidLink = true;
            return Page();
        }

        var result = await userManager.ResetPasswordAsync(user, decodedToken, NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "InvalidToken"))
            {
                InvalidLink = true;
                return Page();
            }

            ErrorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            return Page();
        }

        await authorizationManager.RevokeBySubjectAsync(UserId.ToString());

        await loginAuditService.RecordAsync(
            LoginAuditEventType.PasswordResetCompleted,
            LoginAuditStatus.Success,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.Headers.UserAgent.ToString(),
            user.Email ?? UserId.ToString(),
            userId: user.Id);

        Succeeded = true;
        return Page();
    }
}
