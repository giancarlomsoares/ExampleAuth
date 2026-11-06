using ExampleAuth.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace ExampleAuth.Server.Pages;

public class IndexModel(UserManager<ApplicationUser> userManager, IOpenIddictApplicationManager applicationManager) : PageModel
{
    public int UserCount { get; set; }

    public long ClientCount { get; set; }

    public async Task OnGetAsync()
    {
        UserCount = await userManager.Users.CountAsync();
        ClientCount = await applicationManager.CountAsync();
    }
}
