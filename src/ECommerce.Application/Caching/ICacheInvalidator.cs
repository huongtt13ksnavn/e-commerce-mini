namespace ECommerce.Application.Caching;

public interface ICacheInvalidator
{
    IReadOnlyList<string> CacheKeys { get; }
}
