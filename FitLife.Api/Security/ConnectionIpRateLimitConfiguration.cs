using AspNetCoreRateLimit;
using Microsoft.Extensions.Options;

namespace FitLife.Api.Security;

/// <summary>
/// Resolves rate-limit identity from the connection address only. The library's
/// default also trusts a client-supplied <c>X-Real-IP</c> header (even when the
/// option is cleared), which lets a caller reset its limits by rotating the header.
/// Behind a trusted proxy, <c>UseForwardedHeaders</c> sets the connection address first.
/// </summary>
public sealed class ConnectionIpRateLimitConfiguration : RateLimitConfiguration
{
    public ConnectionIpRateLimitConfiguration(
        IOptions<IpRateLimitOptions> ipOptions,
        IOptions<ClientRateLimitOptions> clientOptions)
        : base(ipOptions, clientOptions)
    {
    }

    public override void RegisterResolvers()
    {
        IpResolvers.Clear();
        IpResolvers.Add(new IpConnectionResolveContributor());
    }
}
