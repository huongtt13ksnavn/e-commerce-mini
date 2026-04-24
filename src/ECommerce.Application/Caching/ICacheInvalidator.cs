namespace ECommerce.Application.Caching;

public interface ICacheInvalidator
{
    // Bare keys without infrastructure prefix — matches ICacheable.CacheKey values exactly.
    IReadOnlyList<string> CacheKeys { get; }
}
