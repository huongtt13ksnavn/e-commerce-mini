using ECommerce.Application.Behaviors;
using ECommerce.Application.Caching;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerce.IntegrationTests.Behaviors;

public sealed class CachingBehaviorTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private sealed record CacheableQuery(string Key) : ICacheable
    {
        public string CacheKey => Key;
        public TimeSpan CacheDuration => TimeSpan.FromMinutes(1);
    }

    private sealed record PlainQuery;

    [Fact]
    public async Task CacheMiss_CallsNextAndStoresResult()
    {
        var cache = CreateCache();
        var callCount = 0;
        var behavior = new CachingBehavior<CacheableQuery, string>(
            cache, NullLogger<CachingBehavior<CacheableQuery, string>>.Instance);

        var result = await behavior.Handle(
            new CacheableQuery("miss-key"),
            ct => { callCount++; return Task.FromResult("hello"); },
            CancellationToken.None);

        result.Should().Be("hello");
        callCount.Should().Be(1);
        var stored = await cache.GetStringAsync("miss-key");
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task CacheHit_ReturnsCachedValueWithoutCallingNext()
    {
        var cache = CreateCache();
        await cache.SetStringAsync("hit-key", "\"cached-value\"",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) });
        var callCount = 0;
        var behavior = new CachingBehavior<CacheableQuery, string>(
            cache, NullLogger<CachingBehavior<CacheableQuery, string>>.Instance);

        var result = await behavior.Handle(
            new CacheableQuery("hit-key"),
            ct => { callCount++; return Task.FromResult("fresh"); },
            CancellationToken.None);

        result.Should().Be("cached-value");
        callCount.Should().Be(0);
    }

    [Fact]
    public async Task NonCacheableRequest_PassesThroughUnchanged()
    {
        var cache = CreateCache();
        var callCount = 0;
        var behavior = new CachingBehavior<PlainQuery, string>(
            cache, NullLogger<CachingBehavior<PlainQuery, string>>.Instance);

        var result = await behavior.Handle(
            new PlainQuery(),
            ct => { callCount++; return Task.FromResult("direct"); },
            CancellationToken.None);

        result.Should().Be("direct");
        callCount.Should().Be(1);
    }
}
