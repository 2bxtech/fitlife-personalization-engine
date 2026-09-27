namespace FitLife.Api.Configuration;

/// <summary>Where recommendation results are cached between requests.</summary>
public enum CacheProvider
{
    /// <summary>Redis cache-aside (default).</summary>
    Redis,

    /// <summary>No cache: every read falls through to SQL. No Redis dependency.</summary>
    None
}

public static class CacheMode
{
    public static CacheProvider Read(IConfiguration configuration) =>
        configuration["Cache:Provider"] switch
        {
            null or "Redis" => CacheProvider.Redis,
            "None" => CacheProvider.None,
            _ => throw new InvalidOperationException("Cache:Provider must be Redis or None.")
        };
}
