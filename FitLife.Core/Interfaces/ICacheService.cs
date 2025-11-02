namespace FitLife.Core.Interfaces;

/// <summary>
/// Interface for cache service operations
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    Task<bool> DeleteAsync(string key);
    bool IsConnected { get; }
}
