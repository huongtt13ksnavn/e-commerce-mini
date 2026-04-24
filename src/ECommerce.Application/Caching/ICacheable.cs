namespace ECommerce.Application.Caching;

public interface ICacheable
{
    // Bare key without infrastructure prefix (e.g. "products:all", not "ecommerce:products:all").
    // The IDistributedCache implementation applies any instance prefix transparently.
    string CacheKey { get; }
    TimeSpan CacheDuration { get; }
}
