using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ExampleAuth.Server.Services;

/// <summary>
/// Options for <see cref="PasswordResetTokenProvider{TUser}"/>. A dedicated subclass —
/// rather than a named registration of the base <see cref="DataProtectionTokenProviderOptions"/> —
/// so the *unnamed* <c>IOptions&lt;T&gt;</c> that <see cref="DataProtectorTokenProvider{TUser}"/>
/// actually resolves is naturally scoped to just this provider. A named
/// <c>Configure&lt;DataProtectionTokenProviderOptions&gt;(name, ...)</c> registration is only ever
/// observable via <c>IOptionsMonitor/Snapshot.Get(name)</c>, which the base provider never calls —
/// it only ever reads the unnamed <see cref="IOptions{TOptions}.Value"/>, so that override was
/// silently ignored.
/// </summary>
public class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public PasswordResetTokenProviderOptions()
    {
        TokenLifespan = TimeSpan.FromHours(1);
    }
}

/// <summary>
/// Password-reset-specific token provider. Identical to
/// <see cref="DataProtectorTokenProvider{TUser}"/> except it resolves
/// <see cref="PasswordResetTokenProviderOptions"/> instead of the base
/// <see cref="DataProtectionTokenProviderOptions"/>, so its lifespan can be configured
/// independently of the default token providers (e.g. email confirmation) without relying on
/// named options, which <see cref="DataProtectorTokenProvider{TUser}"/> does not support.
/// </summary>
public class PasswordResetTokenProvider<TUser>(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<TUser>> logger)
    : DataProtectorTokenProvider<TUser>(dataProtectionProvider, options, logger)
    where TUser : class;
