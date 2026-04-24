# Redis Cache for Product Queries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Redis-backed `IDistributedCache` to product read paths via two MediatR pipeline behaviors — `CachingBehavior` (read-through) and `CacheInvalidationBehavior` (write-through eviction) — with graceful fallback to in-memory cache when Redis is unavailable.

**Architecture:** Two marker interfaces (`ICacheable`, `ICacheInvalidator`) in the Application layer let queries and commands opt in to caching without coupling to infrastructure. Both behaviors sit at the innermost pipeline position (after validation) and no-op on requests that don't implement their respective interface. Redis is registered via `IDistributedCache`; when no connection string is configured (e.g., in tests), the DI registration falls back to `MemoryDistributedCache`.

**Tech Stack:** .NET 10, MediatR 12, `Microsoft.Extensions.Caching.StackExchangeRedis` 10.x, `System.Text.Json`, xunit, FluentAssertions.

---

## File Map

| Action | Path | Responsibility |
|--------|------|---------------|
| Create | `src/ECommerce.Application/Caching/ICacheable.cs` | Marker interface for cacheable queries |
| Create | `src/ECommerce.Application/Caching/ICacheInvalidator.cs` | Marker interface for cache-invalidating commands |
| Create | `src/ECommerce.Application/Behaviors/CachingBehavior.cs` | Read-through cache pipeline behavior |
| Create | `src/ECommerce.Application/Behaviors/CacheInvalidationBehavior.cs` | Cache eviction pipeline behavior |
| Create | `tests/ECommerce.IntegrationTests/Behaviors/CachingBehaviorTests.cs` | Unit tests for CachingBehavior |
| Create | `tests/ECommerce.IntegrationTests/Behaviors/CacheInvalidationBehaviorTests.cs` | Unit tests for CacheInvalidationBehavior |
| Create | `tests/ECommerce.IntegrationTests/Products/ProductCacheTests.cs` | Integration tests for cache invalidation flows |
| Modify | `src/ECommerce.Application/Products/Queries/GetProducts/GetProductsQuery.cs` | Implement ICacheable |
| Modify | `src/ECommerce.Application/Products/Queries/GetProduct/GetProductQuery.cs` | Implement ICacheable |
| Modify | `src/ECommerce.Application/Products/Commands/CreateProduct/CreateProductCommand.cs` | Implement ICacheInvalidator |
| Modify | `src/ECommerce.Application/Products/Commands/UpdateProduct/UpdateProductCommand.cs` | Implement ICacheInvalidator |
| Modify | `src/ECommerce.Application/Products/Commands/DeleteProduct/DeleteProductCommand.cs` | Implement ICacheInvalidator |
| Modify | `src/ECommerce.Application/DependencyInjection.cs` | Register CachingBehavior and CacheInvalidationBehavior |
| Modify | `src/ECommerce.Infrastructure/ECommerce.Infrastructure.csproj` | Add Redis package reference |
| Modify | `src/ECommerce.Infrastructure/DependencyInjection.cs` | Register IDistributedCache (Redis or in-memory fallback) |
| Modify | `src/ECommerce.API/appsettings.json` | Add Redis connection string |
| Modify | `src/ECommerce.API/appsettings.Development.json` | Add Redis connection string |
| Modify | `docker-compose.yml` | Add Redis service |

---

## Task 1: Add caching marker interfaces

**Files:**
- Create: `src/ECommerce.Application/Caching/ICacheable.cs`
- Create: `src/ECommerce.Application/Caching/ICacheInvalidator.cs`

- [ ] **Step 1: Create ICacheable**

```csharp
// src/ECommerce.Application/Caching/ICacheable.cs
namespace ECommerce.Application.Caching;

public interface ICacheable
{
    string CacheKey { get; }
    TimeSpan CacheDuration { get; }
}
```

- [ ] **Step 2: Create ICacheInvalidator**

```csharp
// src/ECommerce.Application/Caching/ICacheInvalidator.cs
namespace ECommerce.Application.Caching;

public interface ICacheInvalidator
{
    IReadOnlyList<string> CacheKeys { get; }
}
```

- [ ] **Step 3: Build to confirm no errors**

```bash
dotnet build src/ECommerce.Application/ECommerce.Application.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/ECommerce.Application/Caching/
git commit -m "feat(cache): add ICacheable and ICacheInvalidator marker interfaces"
```

---

## Task 2: Implement CachingBehavior with TDD

**Files:**
- Create: `tests/ECommerce.IntegrationTests/Behaviors/CachingBehaviorTests.cs`
- Create: `src/ECommerce.Application/Behaviors/CachingBehavior.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/ECommerce.IntegrationTests/Behaviors/CachingBehaviorTests.cs
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
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test tests/ECommerce.IntegrationTests --filter "FullyQualifiedName~CachingBehaviorTests" --no-build 2>&1 | tail -20
```

Expected: build error — `CachingBehavior` does not exist yet.

- [ ] **Step 3: Implement CachingBehavior**

```csharp
// src/ECommerce.Application/Behaviors/CachingBehavior.cs
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
                logger.LogDebug("Cache hit for {Key}", cacheable.CacheKey);
                return JsonSerializer.Deserialize<TResponse>(cached)!;
            }
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
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed for {Key}", cacheable.CacheKey);
        }

        return result;
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test tests/ECommerce.IntegrationTests --filter "FullyQualifiedName~CachingBehaviorTests" -v normal 2>&1 | tail -20
```

Expected: `3 passed`.

- [ ] **Step 5: Commit**

```bash
git add src/ECommerce.Application/Behaviors/CachingBehavior.cs tests/ECommerce.IntegrationTests/Behaviors/CachingBehaviorTests.cs
git commit -m "feat(cache): add CachingBehavior with read-through and exception fallthrough"
```

---

## Task 3: Implement CacheInvalidationBehavior with TDD

**Files:**
- Create: `tests/ECommerce.IntegrationTests/Behaviors/CacheInvalidationBehaviorTests.cs`
- Create: `src/ECommerce.Application/Behaviors/CacheInvalidationBehavior.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/ECommerce.IntegrationTests/Behaviors/CacheInvalidationBehaviorTests.cs
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
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test tests/ECommerce.IntegrationTests --filter "FullyQualifiedName~CacheInvalidationBehaviorTests" --no-build 2>&1 | tail -20
```

Expected: build error — `CacheInvalidationBehavior` does not exist yet.

- [ ] **Step 3: Implement CacheInvalidationBehavior**

```csharp
// src/ECommerce.Application/Behaviors/CacheInvalidationBehavior.cs
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
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cache eviction failed for {Key}", key);
            }
        }

        return result;
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test tests/ECommerce.IntegrationTests --filter "FullyQualifiedName~CacheInvalidationBehaviorTests" -v normal 2>&1 | tail -20
```

Expected: `3 passed`.

- [ ] **Step 5: Commit**

```bash
git add src/ECommerce.Application/Behaviors/CacheInvalidationBehavior.cs tests/ECommerce.IntegrationTests/Behaviors/CacheInvalidationBehaviorTests.cs
git commit -m "feat(cache): add CacheInvalidationBehavior with post-handler eviction"
```

---

## Task 4: Mark queries as ICacheable and register behaviors

**Files:**
- Modify: `src/ECommerce.Application/Products/Queries/GetProducts/GetProductsQuery.cs`
- Modify: `src/ECommerce.Application/Products/Queries/GetProduct/GetProductQuery.cs`
- Modify: `src/ECommerce.Application/DependencyInjection.cs`

- [ ] **Step 1: Update GetProductsQuery**

Replace the entire file content:

```csharp
// src/ECommerce.Application/Products/Queries/GetProducts/GetProductsQuery.cs
using ECommerce.Application.Caching;
using ECommerce.Application.Common.Dtos;
using MediatR;

namespace ECommerce.Application.Products.Queries.GetProducts;

public sealed record GetProductsQuery : IRequest<IReadOnlyList<ProductDto>>, ICacheable
{
    public string CacheKey => "products:all";
    public TimeSpan CacheDuration => TimeSpan.FromMinutes(2);
}
```

- [ ] **Step 2: Update GetProductQuery**

Replace the entire file content:

```csharp
// src/ECommerce.Application/Products/Queries/GetProduct/GetProductQuery.cs
using ECommerce.Application.Caching;
using ECommerce.Application.Common.Dtos;
using MediatR;

namespace ECommerce.Application.Products.Queries.GetProduct;

public sealed record GetProductQuery(Guid Id) : IRequest<ProductDto>, ICacheable
{
    public string CacheKey => $"products:{Id}";
    public TimeSpan CacheDuration => TimeSpan.FromMinutes(2);
}
```

- [ ] **Step 3: Register behaviors in Application DI**

Replace the entire file content:

```csharp
// src/ECommerce.Application/DependencyInjection.cs
using ECommerce.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
```

- [ ] **Step 4: Build Application project**

```bash
dotnet build src/ECommerce.Application/ECommerce.Application.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/ECommerce.Application/Products/Queries/ src/ECommerce.Application/DependencyInjection.cs
git commit -m "feat(cache): mark product queries as ICacheable, register pipeline behaviors"
```

---

## Task 5: Mark commands as ICacheInvalidator

**Files:**
- Modify: `src/ECommerce.Application/Products/Commands/CreateProduct/CreateProductCommand.cs`
- Modify: `src/ECommerce.Application/Products/Commands/UpdateProduct/UpdateProductCommand.cs`
- Modify: `src/ECommerce.Application/Products/Commands/DeleteProduct/DeleteProductCommand.cs`

- [ ] **Step 1: Update CreateProductCommand**

Replace the entire file content:

```csharp
// src/ECommerce.Application/Products/Commands/CreateProduct/CreateProductCommand.cs
using ECommerce.Application.Caching;
using MediatR;

namespace ECommerce.Application.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string Description,
    decimal Price,
    int Stock,
    string? ImageUrl) : IRequest<Guid>, ICacheInvalidator
{
    public IReadOnlyList<string> CacheKeys => ["products:all"];
}
```

- [ ] **Step 2: Update UpdateProductCommand**

Replace the entire file content:

```csharp
// src/ECommerce.Application/Products/Commands/UpdateProduct/UpdateProductCommand.cs
using ECommerce.Application.Caching;
using MediatR;

namespace ECommerce.Application.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int Stock,
    string? ImageUrl) : IRequest, ICacheInvalidator
{
    public IReadOnlyList<string> CacheKeys => ["products:all", $"products:{Id}"];
}
```

- [ ] **Step 3: Update DeleteProductCommand**

Replace the entire file content:

```csharp
// src/ECommerce.Application/Products/Commands/DeleteProduct/DeleteProductCommand.cs
using ECommerce.Application.Caching;
using MediatR;

namespace ECommerce.Application.Products.Commands.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : IRequest, ICacheInvalidator
{
    public IReadOnlyList<string> CacheKeys => ["products:all", $"products:{Id}"];
}
```

- [ ] **Step 4: Build and run all behavior unit tests**

```bash
dotnet build src/ECommerce.Application/ECommerce.Application.csproj && dotnet test tests/ECommerce.IntegrationTests --filter "FullyQualifiedName~BehaviorTests" -v normal 2>&1 | tail -20
```

Expected: `Build succeeded.` and `6 passed`.

- [ ] **Step 5: Commit**

```bash
git add src/ECommerce.Application/Products/Commands/
git commit -m "feat(cache): mark product commands as ICacheInvalidator"
```

---

## Task 6: Infrastructure — Redis registration and configuration

**Files:**
- Modify: `src/ECommerce.Infrastructure/ECommerce.Infrastructure.csproj`
- Modify: `src/ECommerce.Infrastructure/DependencyInjection.cs`
- Modify: `src/ECommerce.API/appsettings.json`
- Modify: `src/ECommerce.API/appsettings.Development.json`
- Modify: `docker-compose.yml`

- [ ] **Step 1: Add Redis package**

In `src/ECommerce.Infrastructure/ECommerce.Infrastructure.csproj`, add inside the second `<ItemGroup>` (with the other `PackageReference` entries):

```xml
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="10.*" />
```

The full ItemGroup should look like:

```xml
<ItemGroup>
  <PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="9.0.0" />
  <PackageReference Include="MediatR" Version="12.*" />
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.*" />
  <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.*" />
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.*" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.*">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
  <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="10.*" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.*" />
  <PackageReference Include="Polly" Version="8.6.6" />
</ItemGroup>
```

- [ ] **Step 2: Register IDistributedCache in Infrastructure DI**

In `src/ECommerce.Infrastructure/DependencyInjection.cs`, add the Redis/fallback registration block just after the `services.AddSingleton<AuditInterceptor>();` line (before `services.AddDbContext`):

```csharp
var redisConnectionString = configuration.GetConnectionString("Redis");
if (redisConnectionString is not null)
    services.AddStackExchangeRedisCache(options =>
        options.Configuration = redisConnectionString);
else
    services.AddDistributedMemoryCache();
```

Also add `using Microsoft.Extensions.Caching.StackExchangeRedis;` is not needed — `AddStackExchangeRedisCache` is an extension method available after adding the package, no extra using required beyond `Microsoft.Extensions.DependencyInjection`.

The top of `DependencyInjection.cs` already has `using Microsoft.Extensions.DependencyInjection;` — no new usings needed.

- [ ] **Step 3: Add Redis connection string to appsettings.json**

Replace the `"ConnectionStrings"` block in `src/ECommerce.API/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=ecommerce;Username=postgres;Password=postgres",
  "Redis": "localhost:6379"
},
```

- [ ] **Step 4: Add Redis connection string to appsettings.Development.json**

Replace the `"ConnectionStrings"` block in `src/ECommerce.API/appsettings.Development.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=ecommerce;Username=postgres;Password=postgres",
  "Redis": "localhost:6379"
},
```

- [ ] **Step 5: Add Redis service to docker-compose.yml**

Add the Redis service after the `db` service block (before the `api` service):

```yaml
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 5s
      retries: 10
```

Update the `api` service's `depends_on` block to include Redis:

```yaml
    depends_on:
      db:
        condition: service_healthy
      redis:
        condition: service_healthy
```

Update the `api` service's `environment` block to pass the Redis connection string:

```yaml
      ConnectionStrings__Redis: "redis:6379"
```

- [ ] **Step 6: Build Infrastructure project**

```bash
dotnet build src/ECommerce.Infrastructure/ECommerce.Infrastructure.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src/ECommerce.Infrastructure/ECommerce.Infrastructure.csproj src/ECommerce.Infrastructure/DependencyInjection.cs src/ECommerce.API/appsettings.json src/ECommerce.API/appsettings.Development.json docker-compose.yml
git commit -m "feat(cache): register Redis IDistributedCache with in-memory fallback"
```

---

## Task 7: Integration tests for cache invalidation

**Files:**
- Create: `tests/ECommerce.IntegrationTests/Products/ProductCacheTests.cs`

The integration tests use `AppFactory` as-is — no Redis connection string is set, so `AddDistributedMemoryCache()` is used automatically. Cache behavior is still tested correctly because the behavior code is identical regardless of backing store.

- [ ] **Step 1: Write integration tests**

```csharp
// tests/ECommerce.IntegrationTests/Products/ProductCacheTests.cs
using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.Common.Dtos;
using FluentAssertions;

namespace ECommerce.IntegrationTests.Products;

[Collection("ProductCache")]
public sealed class ProductCacheTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private static readonly Guid AdminId = Guid.NewGuid();
    private HttpClient AdminClient() => factory.CreateAuthenticatedClient(AdminId.ToString(), "Admin");

    [Fact]
    public async Task GetProducts_SecondCall_ReturnsSameData()
    {
        var client = factory.CreateClient();

        var first = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
        var second = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        second!.Count.Should().Be(first!.Count);
    }

    [Fact]
    public async Task CreateProduct_ThenGetProducts_ReturnsNewProduct()
    {
        var admin = AdminClient();
        var uniqueName = $"CacheTest-{Guid.NewGuid():N}";

        var create = await admin.PostAsJsonAsync("/api/products", new
        {
            Name = uniqueName,
            Description = "Cache invalidation test",
            Price = 9.99m,
            Stock = 5,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var products = await factory.CreateClient().GetFromJsonAsync<List<ProductDto>>("/api/products");

        products.Should().Contain(p => p.Name == uniqueName);
    }

    [Fact]
    public async Task UpdateProduct_ThenGetProductById_ReturnsUpdatedData()
    {
        var admin = AdminClient();

        var create = await admin.PostAsJsonAsync("/api/products", new
        {
            Name = $"Before-{Guid.NewGuid():N}",
            Description = "Original",
            Price = 10.00m,
            Stock = 1,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        var id = created!["id"];

        var firstGet = await factory.CreateClient().GetFromJsonAsync<ProductDto>($"/api/products/{id}");
        firstGet.Should().NotBeNull();

        await admin.PutAsJsonAsync($"/api/products/{id}", new
        {
            Name = "After-Update",
            Description = "Updated",
            Price = 20.00m,
            Stock = 2,
        });

        var secondGet = await factory.CreateClient().GetFromJsonAsync<ProductDto>($"/api/products/{id}");
        secondGet!.Name.Should().Be("After-Update");
        secondGet.Price.Should().Be(20.00m);
    }
}
```

- [ ] **Step 2: Run all tests**

```bash
dotnet test tests/ECommerce.IntegrationTests -v normal 2>&1 | tail -30
```

Expected: all tests pass including the 3 new cache tests.

- [ ] **Step 3: Commit**

```bash
git add tests/ECommerce.IntegrationTests/Products/ProductCacheTests.cs
git commit -m "test(cache): add integration tests for cache invalidation on product mutations"
```

---

## Self-Review Checklist

- [x] **ICacheable / ICacheInvalidator interfaces** — Task 1 ✓
- [x] **CachingBehavior: cache miss, cache hit, exception fallthrough** — Task 2 ✓
- [x] **CacheInvalidationBehavior: evict after success, skip on handler throw** — Task 3 ✓
- [x] **GetProductsQuery implements ICacheable** (`products:all`, 2 min) — Task 4 ✓
- [x] **GetProductQuery implements ICacheable** (`products:{Id}`, 2 min) — Task 4 ✓
- [x] **Behaviors registered after ValidationBehavior** — Task 4 ✓
- [x] **CreateProductCommand invalidates `products:all`** — Task 5 ✓
- [x] **UpdateProductCommand invalidates `products:all` + `products:{Id}`** — Task 5 ✓
- [x] **DeleteProductCommand invalidates `products:all` + `products:{Id}`** — Task 5 ✓
- [x] **Redis package added to Infrastructure** — Task 6 ✓
- [x] **In-memory fallback when Redis connection string absent** — Task 6 ✓
- [x] **appsettings.json + appsettings.Development.json updated** — Task 6 ✓
- [x] **docker-compose.yml Redis service + healthcheck + API dependency** — Task 6 ✓
- [x] **Integration tests: create invalidates list, update invalidates by-id** — Task 7 ✓
- [x] **Error handling: cache read failure falls through, write failure logs warning** — Task 2 ✓
