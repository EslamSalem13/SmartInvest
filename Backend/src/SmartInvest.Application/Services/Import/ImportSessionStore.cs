using Microsoft.Extensions.Caching.Memory;

namespace SmartInvest.Application.Services.Import;

public class ImportSessionStore
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public ImportSessionStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Save(ParsedImportFile file)
    {
        var importId = Guid.NewGuid().ToString("N");
        _cache.Set(CacheKey(importId), file, Ttl);
        return importId;
    }

    public ParsedImportFile? Get(string importId)
    {
        return _cache.TryGetValue(CacheKey(importId), out ParsedImportFile? file) ? file : null;
    }

    public void Remove(string importId)
    {
        _cache.Remove(CacheKey(importId));
    }

    private static string CacheKey(string importId) => $"import-session:{importId}";
}
