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
        var behavior = new CacheInvalidationBehavior<InvalidatingCommand, Unit>(
            cache, NullLogger<CacheInvalidationBehavior<InvalidatingCommand, Unit>>.Instance);

        await behavior.Handle(
            new InvalidatingCommand(["products:all", "products:abc"]),
            ct => Task.FromResult(Unit.Value),
            CancellationToken.None);

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

        await behavior.Handle(
            new PlainCommand(),
            ct => { callCount++; return Task.FromResult(Unit.Value); },
            CancellationToken.None);

        callCount.Should().Be(1);
    }
}
