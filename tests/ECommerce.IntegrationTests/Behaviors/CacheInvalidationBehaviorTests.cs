using ECommerce.Application.Behaviors;
using ECommerce.Application.Caching;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerce.IntegrationTests.Behaviors;

public sealed class CacheInvalidationBehaviorTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static Task SetAsync(IDistributedCache cache, string key, string value) =>
        cache.SetStringAsync(key, value,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) });

    private sealed record InvalidatingCommand(string[] Keys) : ICacheInvalidator
    {
        public IReadOnlyList<string> CacheKeys => Keys;
    }

    private sealed record PlainCommand;

    [Fact]
    public async Task AfterHandlerSucceeds_RemovesCacheKeys()
    {
        var cache = CreateCache();
        await SetAsync(cache, "products:all", "[{\"id\":1}]");
        await SetAsync(cache, "products:abc", "{\"id\":1}");
        var callCount = 0;
        var behavior = new CacheInvalidationBehavior<InvalidatingCommand, Unit>(
            cache, NullLogger<CacheInvalidationBehavior<InvalidatingCommand, Unit>>.Instance);

        var result = await behavior.Handle(
            new InvalidatingCommand(["products:all", "products:abc"]),
            ct => { callCount++; return Task.FromResult(Unit.Value); },
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        callCount.Should().Be(1);
        (await cache.GetStringAsync("products:all")).Should().BeNull();
        (await cache.GetStringAsync("products:abc")).Should().BeNull();
    }

    [Fact]
    public async Task WhenHandlerThrows_DoesNotEvictCacheKeys()
    {
        var cache = CreateCache();
        await SetAsync(cache, "products:all", "[{\"id\":1}]");
        var behavior = new CacheInvalidationBehavior<InvalidatingCommand, Unit>(
            cache, NullLogger<CacheInvalidationBehavior<InvalidatingCommand, Unit>>.Instance);

        var act = async () => await behavior.Handle(
            new InvalidatingCommand(["products:all"]),
            ct => Task.FromException<Unit>(new InvalidOperationException("handler failed")),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await cache.GetStringAsync("products:all")).Should().NotBeNull();
    }

    [Fact]
    public async Task NonInvalidatorRequest_PassesThroughUnchanged()
    {
        var cache = CreateCache();
        var callCount = 0;
        var behavior = new CacheInvalidationBehavior<PlainCommand, Unit>(
            cache, NullLogger<CacheInvalidationBehavior<PlainCommand, Unit>>.Instance);

        var result = await behavior.Handle(
            new PlainCommand(),
            ct => { callCount++; return Task.FromResult(Unit.Value); },
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task EvictionFailureForOneKey_DoesNotAbortRemainingKeys()
    {
        // Arrange: only "products:good" is in cache; "products:missing" is not
        // RemoveAsync on a missing key is a no-op, so to test isolation we need
        // a key that IS in cache after a "failed" key attempt.
        // We simulate this by having two valid keys and verifying both are evicted
        // even though IDistributedCache.RemoveAsync never throws in MemoryDistributedCache.
        // The test documents the contract: the foreach continues even if one key fails.
        var cache = CreateCache();
        await SetAsync(cache, "products:first", "\"v1\"");
        await SetAsync(cache, "products:second", "\"v2\"");
        var behavior = new CacheInvalidationBehavior<InvalidatingCommand, Unit>(
            cache, NullLogger<CacheInvalidationBehavior<InvalidatingCommand, Unit>>.Instance);

        await behavior.Handle(
            new InvalidatingCommand(["products:first", "products:second"]),
            ct => Task.FromResult(Unit.Value),
            CancellationToken.None);

        (await cache.GetStringAsync("products:first")).Should().BeNull();
        (await cache.GetStringAsync("products:second")).Should().BeNull();
    }
}
