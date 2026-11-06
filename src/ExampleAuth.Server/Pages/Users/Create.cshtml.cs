using ExampleAuth.Server.Data;
using ExampleAuth.Server.Models;
using ExampleAuth.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;

namespace ExampleAuth.Server.Pages.Users;

public class CreateModel(
    UserManager<ApplicationUser> userManager,
    ClientAccessService clientAccessService,
    IOpenIddictApplicationManager applicationManager,
    EmailConfirmationService emailConfirmationService) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string? DisplayName { get; set; }

    [BindProperty]
    public List<string> ClientIds { get; set; } = [];

    [BindProperty]
    public string SelectedRole { get; set; } = "None";

    public List<ClientOption> AllClients { get; } = [];

    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadClientsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await userManager.FindByEmailAsync(Email) is not null)
        {
            ErrorMessage = "Já existe um usuário com esse email.";
            await LoadClientsAsync();
            return Page();
        }

        var adminUserId = Guid.Parse(userManager.GetUserId(User)!);

        var newUser = new ApplicationUser
        {
            UserName = Email,
            Email = Email,
            EmailConfirmed = false,
            DisplayName = DisplayName,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = adminUserId,
        };

        var result = await userManager.CreateAsync(newUser, Password);
        if (!result.Succeeded)
        {
            ErrorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            await LoadClientsAsync();
            return Page();
        }

        await emailConfirmationService.SendAsync(newUser);

        if (User.IsInRole(DbInitializer.AdministratorRole))
        {
            foreach (var clientId in ClientIds)
            {
                await clientAccessService.GrantAsync(newUser.Id, clientId, adminUserId);
            }

            if (SelectedRole is DbInitializer.AdministratorRole or DbInitializer.SupportRole)
            {
                await userManager.AddToRoleAsync(newUser, SelectedRole);
            }
        }

        return RedirectToPage("Details", new { id = newUser.Id });
    }

    private async Task LoadClientsAsync()
    {
        AllClients.Clear();
        await foreach (var application in applicationManager.ListAsync())
        {
            AllClients.Add(new ClientOption(
                (await applicationManager.GetClientIdAsync(application))!,
                await applicationManager.GetDisplayNameAsync(application)));
        }
    }
}
