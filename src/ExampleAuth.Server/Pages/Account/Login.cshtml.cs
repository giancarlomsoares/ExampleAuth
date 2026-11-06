using ExampleAuth.Server.Models;
using ExampleAuth.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExampleAuth.Server.Pages.Account;

public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    LoginAuditService loginAuditService) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        returnUrl ??= Url.Content("~/");

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();

        var existingUser = await userManager.FindByEmailAsync(Email);
        if (existingUser is not null && existingUser.IsDisabled)
        {
            ErrorMessage = "Conta desabilitada. Fale com um administrador.";

            await loginAuditService.RecordAsync(
                LoginAuditEventType.PasswordLogin,
                LoginAuditStatus.Failure,
                ipAddress,
                userAgent,
                Email,
                userId: existingUser.Id,
                failureReason: "account_disabled");

            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(Email, Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            var user = await userManager.FindByEmailAsync(Email);
            await loginAuditService.RecordAsync(
                LoginAuditEventType.PasswordLogin,
                LoginAuditStatus.Success,
                ipAddress,
                userAgent,
                Email,
                userId: user?.Id);

            return LocalRedirect(returnUrl);
        }

        ErrorMessage = result.IsLockedOut
            ? "Conta bloqueada temporariamente após várias tentativas inválidas."
            : "E-mail ou senha inválidos.";

        await loginAuditService.RecordAsync(
            LoginAuditEventType.PasswordLogin,
            LoginAuditStatus.Failure,
            ipAddress,
            userAgent,
            Email,
            failureReason: result.IsLockedOut ? "locked_out" : "invalid_credentials");

        return Page();
    }
}
