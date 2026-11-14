using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using StackExchange.Redis;

namespace PushpakClasses.Infrastructure.Services;

public class RedisService : IRedisService
{
    private readonly ILogger<RedisService> _logger;
    private readonly RedisSettings _settings;
    private readonly Lazy<ConnectionMultiplexer?> _lazyConnection;
    private readonly ConcurrentDictionary<string, (string Value, DateTime? Expiry)> _inMemoryFallback = new();

    public RedisService(IOptions<RedisSettings> settings, ILogger<RedisService> logger)
    {
        _logger = logger;
        _settings = settings.Value;

        _lazyConnection = new Lazy<ConnectionMultiplexer?>(() =>
        {
            if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.ConnectionString))
            {
                _logger.LogInformation("Redis is disabled or connection string not configured; using high-speed in-memory fallback.");
                return null;
            }

            try
            {
                var configurationOptions = ConfigurationOptions.Parse(_settings.ConnectionString);
                configurationOptions.AbortOnConnectFail = false;
                configurationOptions.ConnectTimeout = 3000;
                configurationOptions.AsyncTimeout = 3000;
                return ConnectionMultiplexer.Connect(configurationOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to Redis at '{ConnectionString}'. Using in-memory fallback.", _settings.ConnectionString);
                return null;
            }
        });
    }

    private IDatabase? GetDatabase()
    {
        try
        {
            var connection = _lazyConnection.Value;
            if (connection != null && connection.IsConnected)
            {
                return connection.GetDatabase();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting Redis database. Falling back to in-memory cache.");
        }

        return null;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db != null)
        {
            try
            {
                var redisValue = await db.StringGetAsync(key);
                if (redisValue.HasValue)
                {
                    return JsonSerializer.Deserialize<T>((string)redisValue!);
                }
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis StringGetAsync failed for key '{Key}'. Checking in-memory fallback.", key);
            }
        }

        // In-memory fallback
        if (_inMemoryFallback.TryGetValue(key, out var cached))
        {
            if (cached.Expiry.HasValue && cached.Expiry.Value < DateTime.UtcNow)
            {
                _inMemoryFallback.TryRemove(key, out _);
                return default;
            }

            return JsonSerializer.Deserialize<T>(cached.Value);
        }

        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);
        var db = GetDatabase();
        if (db != null)
        {
            try
            {
                await db.StringSetAsync(key, json, expiry, When.Always);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis StringSetAsync failed for key '{Key}'. Storing in in-memory fallback.", key);
            }
        }

        // In-memory fallback
        var expiryTime = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
        _inMemoryFallback[key] = (json, expiryTime);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db != null)
        {
            try
            {
                await db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis KeyDeleteAsync failed for key '{Key}'.", key);
            }
        }

        _inMemoryFallback.TryRemove(key, out _);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db != null)
        {
            try
            {
                return await db.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis KeyExistsAsync failed for key '{Key}'.", key);
            }
        }

        if (_inMemoryFallback.TryGetValue(key, out var cached))
        {
            if (cached.Expiry.HasValue && cached.Expiry.Value < DateTime.UtcNow)
            {
                _inMemoryFallback.TryRemove(key, out _);
                return false;
            }
            return true;
        }

        return false;
    }
}
