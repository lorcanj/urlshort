using StackExchange.Redis;
using System.Text.Json;

namespace urlshort.Services;
public class CacheService : ICacheService
{
    private readonly IDatabase _cachedDb;

    public CacheService()
    {
        var redis = ConnectionMultiplexer.Connect("localhost:6379");
        _cachedDb = redis.GetDatabase();
    }

    public T? GetData<T>(string key)
    {
        var value = _cachedDb.StringGet(key);
        if (!string.IsNullOrEmpty(value)) return JsonSerializer.Deserialize<T>(value);

        return default;
    }

    public object RemoveData(string key)
    {
        var _exists = _cachedDb.KeyExists(key);

        if (_exists) return _cachedDb.KeyDelete(key);

        return false;
    }
    // probably want to await some of this stuff to ensure that it's been done before
    // moving on
    public bool SetData<T>(string key, T value, DateTimeOffset expirationTime)
    {
        var expiryTime = expirationTime.DateTime.Subtract(DateTime.Now);
        return _cachedDb.StringSet(key, JsonSerializer.Serialize(value), expiryTime);
    }
}

