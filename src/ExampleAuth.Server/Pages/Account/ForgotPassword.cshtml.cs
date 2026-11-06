using ExampleAuth.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace ExampleAuth.Server.Pages.Account;

[EnableRateLimiting("PasswordReset")]
public class ForgotPasswordModel(PasswordResetService passwordResetService) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    public bool Submitted { get; private set; }

    public void OnGet()
    {
    }

    public async Task OnPostAsync()
    {
        if (!string.IsNullOrWhiteSpace(Email))
        {
            await passwordResetService.SendAsync(Email.Trim(), HttpContext);
        }

        Submitted = true;
    }
}
