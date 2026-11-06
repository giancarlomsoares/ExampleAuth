using ExampleAuth.Server.Data;
using ExampleAuth.Server.Models;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ExampleAuth.Server.Services;

public class ClientAccessService(
    ExampleAuthDbContext db,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    LoginAuditService loginAuditService)
{
    public async Task<List<ClientAccessGrant>> ListForClientAsync(string clientId)
    {
        var applicationId = await ResolveApplicationIdAsync(clientId);
        if (applicationId is null)
        {
            return [];
        }

        return await db.ClientAccessGrants
            .Where(g => g.ApplicationId == applicationId.Value && g.RevokedAtUtc == null)
            .ToListAsync();
    }

    public Task<List<ClientAccessGrant>> ListForUserAsync(Guid userId)
    {
        return db.ClientAccessGrants
            .Where(g => g.UserId == userId && g.RevokedAtUtc == null)
            .ToListAsync();
    }

    public async Task<bool> HasAccessAsync(Guid userId, string applicationId)
    {
        var appGuid = Guid.Parse(applicationId);
        return await db.ClientAccessGrants
            .AnyAsync(g => g.UserId == userId && g.ApplicationId == appGuid && g.RevokedAtUtc == null);
    }

    public async Task<bool> GrantAsync(Guid userId, string clientId, Guid grantedByUserId)
    {
        var applicationId = await ResolveApplicationIdAsync(clientId);
        if (applicationId is null)
        {
            return false;
        }

        var exists = await db.ClientAccessGrants
            .AnyAsync(g => g.UserId == userId && g.ApplicationId == applicationId.Value && g.RevokedAtUtc == null);
        if (exists)
        {
            return true;
        }

        db.ClientAccessGrants.Add(new ClientAccessGrant
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationId = applicationId.Value,
            GrantedByUserId = grantedByUserId,
            GrantedAtUtc = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();

        return true;
    }

    public async Task RevokeAsync(Guid userId, string clientId, Guid revokedByUserId, string ipAddress, string? userAgent)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId);
        if (application is null)
        {
            return;
        }

        var applicationIdString = await applicationManager.GetIdAsync(application)
            ?? throw new InvalidOperationException("The application id cannot be retrieved.");
        var applicationId = Guid.Parse(applicationIdString);

        var grant = await db.ClientAccessGrants
            .FirstOrDefaultAsync(g => g.UserId == userId && g.ApplicationId == applicationId && g.RevokedAtUtc == null);
        if (grant is not null)
        {
            grant.RevokedAtUtc = DateTimeOffset.UtcNow;
            grant.RevokedByUserId = revokedByUserId;
            await db.SaveChangesAsync();

            await loginAuditService.RecordAsync(
                LoginAuditEventType.ClientAccessRevoked,
                LoginAuditStatus.Success,
                ipAddress,
                userAgent,
                userId.ToString(),
                userId: userId,
                clientId: applicationIdString);
        }

        var authorizations = await authorizationManager.FindAsync(
            subject: userId.ToString(),
            client: applicationIdString,
            status: Statuses.Valid,
            type: null,
            scopes: null).ToListAsync();

        foreach (var authorization in authorizations)
        {
            await authorizationManager.TryRevokeAsync(authorization);
        }
    }

    private async Task<Guid?> ResolveApplicationIdAsync(string clientId)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId);
        if (application is null)
        {
            return null;
        }

        var applicationId = await applicationManager.GetIdAsync(application)
            ?? throw new InvalidOperationException("The application id cannot be retrieved.");
        return Guid.Parse(applicationId);
    }
}
