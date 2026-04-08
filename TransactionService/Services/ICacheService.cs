using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace TransactionService.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    Task SetAsync<T>(string key, T value, int ttlMinutes, CancellationToken cancellationToken = default) where T : class;
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public CacheService(IDistributedCache cache, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var cached = await _cache.GetStringAsync(key, cancellationToken);
            if (cached is null)
            {
                _logger.LogDebug("[Cache][Miss] Key: {CacheKey}", key);
                return null;
            }

            _logger.LogDebug("[Cache][Hit] Key: {CacheKey}", key);
            return JsonSerializer.Deserialize<T>(cached, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Cache][Error] Failed to get key: {CacheKey}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, int ttlMinutes, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ttlMinutes)
            };

            await _cache.SetStringAsync(key, json, options, cancellationToken);
            _logger.LogDebug("[Cache][Set] Key: {CacheKey}, TTL: {TtlMinutes}min", key, ttlMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Cache][Error] Failed to set key: {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
            _logger.LogDebug("[Cache][Invalidate] Key: {CacheKey}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Cache][Error] Failed to remove key: {CacheKey}", key);
        }
    }
}
