using ExampleAuth.Server.Data;
using ExampleAuth.Server.Models;
using ExampleAuth.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ExampleAuth.Server.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "ClientUserManagement")]
public class ClientUsersController(
    UserManager<ApplicationUser> userManager,
    ClientAccessService clientAccessService,
    IOpenIddictApplicationManager applicationManager,
    EmailConfirmationService emailConfirmationService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientUserRequest request)
    {
        // Confirms the caller's `sub` resolves to a real registered client — rejects human-subject tokens
        // that could otherwise carry the `users.manage` scope. Throws (500) if it doesn't resolve.
        await ResolveCallerApplicationIdAsync();

        var actingUser = await userManager.FindByIdAsync(request.ActingUserId.ToString());
        if (actingUser is null)
        {
            return ValidationProblem(ToModelState(new Dictionary<string, string[]> { ["actingUserId"] = ["Usuário não encontrado."] }));
        }

        var newUser = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = false,
            DisplayName = request.DisplayName,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = request.ActingUserId,
        };

        var result = await userManager.CreateAsync(newUser, request.Password);
        if (!result.Succeeded)
        {
            return ValidationProblem(ToModelState(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        }

        await emailConfirmationService.SendAsync(newUser);

        var clientId = ResolveCallerClientId();
        await clientAccessService.GrantAsync(newUser.Id, clientId, request.ActingUserId);

        return Created($"/api/users/{newUser.Id}", new ClientUserResponse(newUser.Id, newUser.Email!, newUser.DisplayName));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientUserRequest request)
    {
        var applicationId = await ResolveCallerApplicationIdAsync();

        var actingUser = await userManager.FindByIdAsync(request.ActingUserId.ToString());
        if (actingUser is null)
        {
            return ValidationProblem(ToModelState(new Dictionary<string, string[]> { ["actingUserId"] = ["Usuário não encontrado."] }));
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null
            || await userManager.IsInRoleAsync(user, DbInitializer.AdministratorRole)
            || await userManager.IsInRoleAsync(user, DbInitializer.SupportRole)
            || !await clientAccessService.HasAccessAsync(id, applicationId))
        {
            return NotFound();
        }

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName;
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return ValidationProblem(ToModelState(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        }

        return Ok(new ClientUserResponse(user.Id, user.Email!, user.DisplayName));
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetClientUserPasswordRequest request)
    {
        var applicationId = await ResolveCallerApplicationIdAsync();

        var actingUser = await userManager.FindByIdAsync(request.ActingUserId.ToString());
        if (actingUser is null)
        {
            return ValidationProblem(ToModelState(new Dictionary<string, string[]> { ["actingUserId"] = ["Usuário não encontrado."] }));
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null
            || await userManager.IsInRoleAsync(user, DbInitializer.AdministratorRole)
            || await userManager.IsInRoleAsync(user, DbInitializer.SupportRole)
            || !await clientAccessService.HasAccessAsync(id, applicationId))
        {
            return NotFound();
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return ValidationProblem(ToModelState(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}/access")]
    public async Task<IActionResult> RevokeAccess(Guid id, [FromQuery] Guid actingUserId)
    {
        // Confirms the caller's `sub` resolves to a real registered client — rejects human-subject tokens
        // that could otherwise carry the `users.manage` scope. Throws (500) if it doesn't resolve.
        await ResolveCallerApplicationIdAsync();

        var actingUser = await userManager.FindByIdAsync(actingUserId.ToString());
        if (actingUser is null)
        {
            return ValidationProblem(ToModelState(new Dictionary<string, string[]> { ["actingUserId"] = ["Usuário não encontrado."] }));
        }

        var clientId = ResolveCallerClientId();
        await clientAccessService.RevokeAsync(
            id,
            clientId,
            actingUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.Headers.UserAgent.ToString());

        return NoContent();
    }

    private string ResolveCallerClientId() =>
        User.GetClaim(Claims.Subject)
            ?? throw new InvalidOperationException("O client autenticado não possui claim 'sub'.");

    private async Task<string> ResolveCallerApplicationIdAsync()
    {
        var clientId = ResolveCallerClientId();
        var application = await applicationManager.FindByClientIdAsync(clientId)
            ?? throw new InvalidOperationException("O client autenticado não foi encontrado.");
        return await applicationManager.GetIdAsync(application)
            ?? throw new InvalidOperationException("O id da aplicação não pôde ser resolvido.");
    }

    private static ModelStateDictionary ToModelState(Dictionary<string, string[]> errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (key, messages) in errors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError(key, message);
            }
        }

        return modelState;
    }
}

public record CreateClientUserRequest(string Email, string Password, string? DisplayName, Guid ActingUserId);
public record UpdateClientUserRequest(string? DisplayName, Guid ActingUserId);
public record ResetClientUserPasswordRequest(string NewPassword, Guid ActingUserId);

public record ClientUserResponse(Guid Id, string Email, string? DisplayName);
