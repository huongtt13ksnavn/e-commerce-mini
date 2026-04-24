using ECommerce.Application.Caching;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Behaviors;

public sealed class CacheInvalidationBehavior<TRequest, TResponse>(
    IDistributedCache cache,
    ILogger<CacheInvalidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheInvalidator invalidator)
            return await next(cancellationToken);

        var result = await next(cancellationToken);

        foreach (var key in invalidator.CacheKeys)
        {
            try
            {
                await cache.RemoveAsync(key, cancellationToken);
                logger.LogDebug("Evicted cache key {Key}", key);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Cache eviction failed for {Key}", key);
            }
        }

        return result;
    }
}
