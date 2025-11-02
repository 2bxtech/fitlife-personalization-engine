using FitLife.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace FitLife.Infrastructure.Cache;

/// <summary>
/// Redis cache service for recommendation caching and session storage
/// Implements cache-aside pattern with TTL management
/// </summary>
public class RedisCacheService : ICacheService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;
    private bool _disposed = false;

    public RedisCacheService(IConfiguration configuration, ILogger<RedisCacheService> logger)
    {
        _logger = logger;
        
        var connectionString = configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString not configured");

        try
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false; // Retry connection failures
            options.ConnectTimeout = 5000;
            options.SyncTimeout = 5000;
            options.AsyncTimeout = 5000;
            options.ConnectRetry = 3;
            
            _redis = ConnectionMultiplexer.Connect(options);
            _db = _redis.GetDatabase();

            _redis.ConnectionFailed += (sender, args) =>
            {
                _logger.LogError("Redis connection failed: {FailureType} - {Exception}",
                    args.FailureType, args.Exception?.Message);
            };

            _redis.ConnectionRestored += (sender, args) =>
            {
                _logger.LogInformation("Redis connection restored");
            };

            _logger.LogInformation("Redis cache service initialized with connection: {Connection}",
                connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Redis");
            throw;
        }
    }

    /// <summary>
    /// Check if Redis is connected and healthy
    /// </summary>
    public bool IsConnected => _redis?.IsConnected ?? false;

    /// <summary>
    /// Get a value from cache by key
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="key">Cache key</param>
    /// <returns>Cached value or default(T) if not found</returns>
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            
            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return null;
            }

            _logger.LogDebug("Cache hit for key: {Key}", key);
            return JsonSerializer.Deserialize<T>(value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from cache for key: {Key}", key);
            return null;
        }
    }

    /// <summary>
    /// Get a string value from cache
    /// </summary>
    public async Task<string?> GetStringAsync(string key)
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            return value.IsNullOrEmpty ? null : value.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting string from cache for key: {Key}", key);
            return null;
        }
    }

    /// <summary>
    /// Set a value in cache with optional expiration
    /// </summary>
    /// <typeparam name="T">Type of value to cache</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="expiration">Optional expiration time (default: 10 minutes for recommendations)</param>
    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            // Default TTL: 10 minutes for recommendations
            var ttl = expiration ?? TimeSpan.FromMinutes(10);
            var json = JsonSerializer.Serialize(value);
            
            var success = await _db.StringSetAsync(key, json, ttl);
            
            if (success)
            {
                _logger.LogDebug("Cached value for key: {Key} with TTL: {TTL}s", key, ttl.TotalSeconds);
            }
            else
            {
                _logger.LogWarning("Failed to cache value for key: {Key}", key);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in cache for key: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Set a string value in cache
    /// </summary>
    public async Task<bool> SetStringAsync(string key, string value, TimeSpan? expiration = null)
    {
        try
        {
            var ttl = expiration ?? TimeSpan.FromMinutes(10);
            return await _db.StringSetAsync(key, value, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting string in cache for key: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Delete a key from cache
    /// Used for cache invalidation (e.g., when user books a class)
    /// </summary>
    public async Task<bool> DeleteAsync(string key)
    {
        try
        {
            var deleted = await _db.KeyDeleteAsync(key);
            
            if (deleted)
            {
                _logger.LogInformation("Cache invalidated for key: {Key}", key);
            }
            else
            {
                _logger.LogDebug("Key not found in cache: {Key}", key);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting key from cache: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Delete multiple keys matching a pattern
    /// WARNING: Use with caution - can be expensive on large datasets
    /// </summary>
    public async Task<int> DeleteByPatternAsync(string pattern)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern).ToArray();
            
            if (keys.Length == 0)
            {
                _logger.LogDebug("No keys found matching pattern: {Pattern}", pattern);
                return 0;
            }

            var deleted = await _db.KeyDeleteAsync(keys);
            _logger.LogInformation("Deleted {Count} keys matching pattern: {Pattern}", deleted, pattern);
            
            return (int)deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting keys by pattern: {Pattern}", pattern);
            return 0;
        }
    }

    /// <summary>
    /// Check if a key exists in cache
    /// </summary>
    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking key existence: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Get time to live for a key
    /// </summary>
    public async Task<TimeSpan?> GetTtlAsync(string key)
    {
        try
        {
            return await _db.KeyTimeToLiveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TTL for key: {Key}", key);
            return null;
        }
    }

    /// <summary>
    /// Increment a counter (for analytics, rate limiting, etc.)
    /// </summary>
    public async Task<long> IncrementAsync(string key, long value = 1, TimeSpan? expiration = null)
    {
        try
        {
            var result = await _db.StringIncrementAsync(key, value);
            
            // Set expiration if this is the first increment
            if (result == value && expiration.HasValue)
            {
                await _db.KeyExpireAsync(key, expiration.Value);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing key: {Key}", key);
            return 0;
        }
    }

    /// <summary>
    /// Add to a sorted set (for popular classes ranking, leaderboards)
    /// </summary>
    public async Task<bool> SortedSetAddAsync(string key, string member, double score)
    {
        try
        {
            return await _db.SortedSetAddAsync(key, member, score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding to sorted set: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Get top N items from sorted set (descending order)
    /// </summary>
    public async Task<List<string>> SortedSetRangeAsync(string key, long start = 0, long stop = -1)
    {
        try
        {
            var values = await _db.SortedSetRangeByRankAsync(key, start, stop, Order.Descending);
            return values.Select(v => v.ToString()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sorted set range: {Key}", key);
            return new List<string>();
        }
    }

    /// <summary>
    /// Ping Redis to check connectivity
    /// </summary>
    public async Task<TimeSpan> PingAsync()
    {
        try
        {
            return await _db.PingAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pinging Redis");
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _redis?.Dispose();
            _logger.LogInformation("Redis cache service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing Redis connection");
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
