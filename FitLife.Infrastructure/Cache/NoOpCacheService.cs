using FitLife.Core.Interfaces;

namespace FitLife.Infrastructure.Cache;

/// <summary>
/// Cache used when <c>Cache:Provider=None</c>: nothing is stored, so every read is a
/// miss and callers fall through to SQL (recent persisted recommendations, then
/// regeneration). Invalidation trivially succeeds because nothing can be stale.
/// </summary>
public sealed class NoOpCacheService : ICacheService
{
    public bool IsConnected => false;

    public Task<T?> GetAsync<T>(string key) where T : class => Task.FromResult<T?>(null);

    public Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class =>
        Task.FromResult(false);

    public Task<bool> DeleteAsync(string key) => Task.FromResult(true);
}
