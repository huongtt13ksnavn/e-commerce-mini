using System.Text.Json;
using ECommerce.Application.Caching;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Behaviors;

public sealed class CachingBehavior<TRequest, TResponse>(
    IDistributedCache cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheable cacheable)
            return await next(cancellationToken);

        try
        {
            var cached = await cache.GetStringAsync(cacheable.CacheKey, cancellationToken);
            if (cached is not null)
            {
                var deserialized = JsonSerializer.Deserialize<TResponse>(cached);
                if (deserialized is not null)
                {
                    logger.LogDebug("Cache hit for {Key}", cacheable.CacheKey);
                    return deserialized;
                }
                logger.LogDebug("Cached null for {Key}, falling through to handler", cacheable.CacheKey);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for {Key}, falling through to handler", cacheable.CacheKey);
            return await next(cancellationToken);
        }

        var result = await next(cancellationToken);

        try
        {
            var serialized = JsonSerializer.Serialize(result);
            await cache.SetStringAsync(
                cacheable.CacheKey,
                serialized,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = cacheable.CacheDuration },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // cancellation after handler succeeded — result is valid, just don't cache
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed for {Key}", cacheable.CacheKey);
        }

        return result;
    }
}
