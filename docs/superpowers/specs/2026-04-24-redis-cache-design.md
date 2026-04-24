# Redis Cache for Product Queries

**Date:** 2026-04-24  
**Status:** Approved

## Goal

Reduce database load on product read paths by caching `GetProductsQuery` and `GetProductQuery` results in Redis via `IDistributedCache`. Stale stock on listing pages is acceptable; real stock enforcement happens at order-placement.

## Architecture

Two new MediatR pipeline behaviors added at the innermost position (after validation):

```
ExceptionHandlingBehavior → LoggingBehavior → ValidationBehavior → CachingBehavior / CacheInvalidationBehavior → Handler
```

Both behaviors no-op on requests that don't implement their respective marker interface, so both can be registered unconditionally.

Registration order in `AddApplication` (appended after `ValidationBehavior`):

```csharp
cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationBehavior<,>));
```

## Interfaces

Defined in `ECommerce.Application/Caching/`:

```csharp
public interface ICacheable
{
    string CacheKey { get; }
    TimeSpan CacheDuration { get; }
}

public interface ICacheInvalidator
{
    IReadOnlyList<string> CacheKeys { get; }
}
```

Queries opt in by implementing `ICacheable`. Commands opt in by implementing `ICacheInvalidator`. Requests that implement neither pass through the behaviors untouched.

## Behaviors

Both live in `ECommerce.Application/Behaviors/`:

### CachingBehavior

- If `TRequest` does not implement `ICacheable`, call `next()` and return.
- Check `IDistributedCache` for `request.CacheKey`.
- Cache hit: deserialize JSON and return.
- Cache miss: call `next()`, serialize result, store with `AbsoluteExpirationRelativeToNow = request.CacheDuration`, return result.

### CacheInvalidationBehavior

- If `TRequest` does not implement `ICacheInvalidator`, call `next()` and return.
- Call `next()` first (command succeeds before cache is cleared).
- Remove each key in `request.CacheKeys` from `IDistributedCache`.

## Query Changes

Both queries return existing `ProductDto` (unchanged — includes `Stock`).

| Query | Implements | CacheKey | TTL |
|-------|-----------|----------|-----|
| `GetProductsQuery` | `ICacheable` | `products:all` | 2 min |
| `GetProductQuery` | `ICacheable` | `products:{Id}` | 2 min |

`GetProductQuery.CacheKey` interpolates the request `Id`: `$"products:{Id}"`.

## Command Invalidation

| Command | Implements | Invalidates |
|---------|-----------|-------------|
| `CreateProductCommand` | `ICacheInvalidator` | `products:all` |
| `UpdateProductCommand` | `ICacheInvalidator` | `products:all`, `products:{Id}` |
| `DeleteProductCommand` | `ICacheInvalidator` | `products:all`, `products:{Id}` |

## Infrastructure

### Package

Add to `ECommerce.Infrastructure.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="10.*" />
```

### Registration

In `DependencyInjection.AddInfrastructure`:

```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Redis connection string is required.");
});
```

### Configuration

`appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "...",
  "Redis": "localhost:6379"
}
```

`docker-compose.yml` (or dev environment): Redis service on port 6379.

### Serialization

`System.Text.Json` — no extra dependency. Behaviors use `JsonSerializer.Serialize` / `JsonSerializer.Deserialize`.

## Error Handling

Cache failures must not break reads. `CachingBehavior` catches exceptions from `IDistributedCache` operations, logs a warning, and falls through to `next()`. Writes are fire-and-forget on failure (log + continue).

## Testing

- Unit tests for `CachingBehavior`: cache hit returns cached value, cache miss calls handler once and stores result, cache exception falls through.
- Unit tests for `CacheInvalidationBehavior`: keys removed after handler succeeds, handler failure skips invalidation.
- Integration tests for `GetProductsQuery` and `GetProductQuery`: verify cache hit skips DB call, verify `UpdateProductCommand` clears relevant keys.

## Out of Scope

- Caching cart or order queries.
- Distributed cache invalidation across multiple API instances (Redis pub/sub).
- Sliding expiration.
- Cache warming on startup.
