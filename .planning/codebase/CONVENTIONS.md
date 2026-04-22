# Coding Conventions
_Last updated: 2026-04-22_

## Summary
This is a C# / .NET 10 clean-architecture project using CQRS and MediatR. Classes are `sealed` by default, `record` types are used for immutable DTOs and value objects, and primary constructors (C# 12) are used throughout to inject dependencies. All files use file-scoped namespaces and rely on implicit usings with nullable reference types enabled globally.

---

## Language & Runtime

- **Language:** C# 12 on .NET 10
- **Nullable:** `<Nullable>enable</Nullable>` in every project — use `?` for optional values, `!` only where nullability is guaranteed by framework contracts
- **Implicit usings:** Enabled — `System`, `System.Linq`, `System.Threading`, etc. are available without explicit using statements
- All projects target `net10.0`

---

## Naming Patterns

**Files:**
- One top-level type per file; file name matches the type name exactly
- Example: `CreateProductCommand.cs` contains only `CreateProductCommand`

**Classes/Records/Interfaces:**
- `PascalCase` for all types
- Interfaces prefixed with `I`: `IProductRepository`, `IUnitOfWork`, `IJwtTokenGenerator`
- Exception classes suffixed with `Exception`: `NotFoundException`, `DomainException`, `CartEmptyException`
- Command/Query suffix on MediatR messages: `CreateProductCommand`, `GetProductQuery`
- Handler suffix on MediatR handlers: `CreateProductCommandHandler`, `GetProductQueryHandler`
- Validator suffix on FluentValidation validators: `CreateProductCommandValidator`
- DTO/request types suffixed with `Dto`, `Request`, or `Response`: `ProductDto`, `CreateProductRequest`, `LoginResponse`
- Configuration classes suffixed with `Configuration`: `ProductConfiguration`

**Methods:**
- `PascalCase` for public methods
- `camelCase` for local variables and parameters
- Async methods suffixed with `Async`: `CommitAsync`, `GetByIdAsync`, `MigrateAndSeedAsync`
- Private static helpers named descriptively: `ApplyAuditFields`, `WriteProblemAsync`, `SeedAdminAsync`

**Properties:**
- `PascalCase` for all properties
- Boolean properties use `Is` prefix: `IsActive`

---

## Class Design

**Sealed by default:**
Every concrete class is declared `sealed`. Open for inheritance only when explicitly needed (e.g., `AggregateRoot` is `abstract`; domain exceptions inherit from the abstract `DomainException`).

```csharp
public sealed class CreateProductCommandHandler(...) : IRequestHandler<CreateProductCommand, Guid>
public sealed record Money(decimal Amount, string Currency)
public sealed class ProductRepository(AppDbContext dbContext) : IProductRepository
```

**Primary constructors for DI:**
Constructor injection uses C# 12 primary constructors — no backing fields, no assignment boilerplate:

```csharp
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
```

**Records for immutable types:**
DTOs, requests, responses, and value objects are `sealed record` with positional parameters:

```csharp
public sealed record ProductDto(Guid Id, string Name, string Description, decimal Price, string Currency, int Stock, string? ImageUrl, bool IsActive);
public sealed record CreateProductRequest(string Name, string Description, decimal Price, int Stock, string? ImageUrl);
public sealed record Money(decimal Amount, string Currency)
```

**Private parameterless constructor on aggregates:**
Domain entities expose only factory/mutator methods; the EF Core navigation constructor is `private`:

```csharp
public sealed class Product : AggregateRoot
{
    private Product() { }  // for EF Core

    public static Product Create(string name, ...) { ... }
    public void Update(string name, ...) { ... }
    public void Deactivate() => IsActive = false;
}
```

---

## Code Organization

**File-scoped namespaces:** All files use `namespace X.Y.Z;` (not block-scoped `{ }`).

**Namespace mirrors directory structure:**
- `ECommerce.Domain.Entities` → `src/ECommerce.Domain/Entities/`
- `ECommerce.Application.Products.Commands.CreateProduct` → `src/ECommerce.Application/Products/Commands/CreateProduct/`
- `ECommerce.Infrastructure.Persistence.Repositories` → `src/ECommerce.Infrastructure/Persistence/Repositories/`

**One file per CQRS slice:** Each command/query slice lives in its own folder with up to three files:
```
Products/Commands/CreateProduct/
    CreateProductCommand.cs          # IRequest<Guid>
    CreateProductCommandHandler.cs   # IRequestHandler
    CreateProductCommandValidator.cs # AbstractValidator
```

**Dependency injection registration:**
Each project exposes one `static class DependencyInjection` with an `AddX(this IServiceCollection)` extension method:
- `src/ECommerce.Application/DependencyInjection.cs` → `AddApplication()`
- `src/ECommerce.Infrastructure/DependencyInjection.cs` → `AddInfrastructure()`

**Endpoint grouping:**
Minimal API endpoints are static classes with a single `MapXEndpoints(this IEndpointRouteBuilder)` extension method grouped by domain:
- `src/ECommerce.API/Endpoints/ProductEndpoints.cs`
- `src/ECommerce.API/Endpoints/AuthEndpoints.cs`

---

## Import Organization

Imports are ordered as follows within a file (no blank-line separation enforced, alphabetical within each group by convention):
1. Framework/BCL usings (`MediatR`, `Microsoft.*`, `System.*`)
2. Domain project usings (`ECommerce.Domain.*`)
3. Application project usings (`ECommerce.Application.*`)
4. Infrastructure project usings (`ECommerce.Infrastructure.*`)

No aliased imports observed; no static imports.

---

## Error Handling

**Domain exceptions hierarchy:**
All domain-level failures extend the abstract `DomainException` at `src/ECommerce.Domain/Exceptions/DomainException.cs`.

```csharp
public abstract class DomainException(string message) : Exception(message);
public sealed class NotFoundException(string entityName, object id)
    : DomainException($"{entityName} with id '{id}' was not found.");
public sealed class ProductUnavailableException(Guid productId)
    : DomainException($"Product '{productId}' is no longer available.");
public sealed class RegistrationFailedException(string message) : DomainException(message);
public sealed class CartEmptyException() : DomainException("Cart is empty.");
```

**Throw-on-null pattern (?.?? throw):**
Null-check results with inline throw:
```csharp
var product = await productRepository.GetByIdAsync(request.Id, cancellationToken)
    ?? throw new NotFoundException("Product", request.Id);
```

**Guard clauses via BCL helpers:**
Domain entities use `ArgumentException.ThrowIfNullOrWhiteSpace` and `ArgumentOutOfRangeException.ThrowIfNegative` for precondition validation — not `if/throw` blocks.

**MediatR pipeline behaviors (in registration order):**
1. `ExceptionHandlingBehavior` — catches, logs, rethrows; at `src/ECommerce.Application/Behaviors/ExceptionHandlingBehavior.cs`
2. `LoggingBehavior` — structured logging of request name + elapsed ms; at `src/ECommerce.Application/Behaviors/LoggingBehavior.cs`
3. `ValidationBehavior` — runs all FluentValidation validators, throws `ValidationException` on failure; at `src/ECommerce.Application/Behaviors/ValidationBehavior.cs`

**HTTP error mapping (`ExceptionMiddleware`):**
Located at `src/ECommerce.API/Middleware/ExceptionMiddleware.cs`. Maps exceptions to RFC 7807 `ProblemDetails`:

| Exception | HTTP Status |
|---|---|
| `ValidationException` | 422 Unprocessable Entity |
| `NotFoundException` | 404 Not Found |
| `DomainException` | 400 Bad Request |
| `DbUpdateConcurrencyException` | 409 Conflict |
| `DbUpdateException` | 422 Unprocessable Entity |
| `UnauthorizedAccessException` | 401 Unauthorized |
| Anything else | 500 Internal Server Error |

Response body is always `application/problem+json`.

---

## Validation

FluentValidation validators (`AbstractValidator<T>`) are co-located with their command/query in the same folder. All validators are registered via `services.AddValidatorsFromAssembly(assembly)` and run automatically through `ValidationBehavior`.

**Validator conventions:**
- `RuleFor` chains with `.NotEmpty()`, `.MaximumLength()`, `.GreaterThan()`, `.EmailAddress()` etc.
- Conditional rules use `.When(x => x.Property is not null)` guards
- Custom messages use `.WithMessage("...")`

```csharp
RuleFor(x => x.ImageUrl)
    .MaximumLength(500)
    .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps)
    .WithMessage("ImageUrl must be a valid HTTPS URI.")
    .When(x => x.ImageUrl is not null);
```

---

## Logging

**Framework:** `Microsoft.Extensions.Logging` (`ILogger<T>`) — injected via primary constructor.

**Structured logging with message templates:**
```csharp
logger.LogInformation("{Request} completed in {ElapsedMs}ms", requestName, sw.ElapsedMilliseconds);
logger.LogWarning("Validation failed for {Request}: {Errors}", typeof(TRequest).Name, ex.Errors);
logger.LogError(ex, "Unhandled exception for {Request}", typeof(TRequest).Name);
```

**Log levels by severity:**
- `LogInformation` — successful request completion
- `LogWarning` — validation failures and domain rule violations (expected errors)
- `LogError` — unhandled exceptions (unexpected errors)

Logging is performed exclusively in pipeline behaviors and the exception middleware — not in handlers or repositories.

---

## Configuration Validation at Startup

Required configuration values are validated eagerly during startup with `?? throw new InvalidOperationException(...)`:
```csharp
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is required.");
```

This pattern is used in both `Program.cs` and `DependencyInjection.cs`.

---

## Null Handling Patterns

- Nullable reference types enabled globally — use `?` suffix for optional values
- Use `is not null` / `is null` pattern matching, not `!= null`
- Use null-conditional `?.` and null-coalescing `??` operators freely
- `null!` used only where the framework guarantees initialization (EF Core `= null!` for required navigation properties)

---

*Convention analysis: 2026-04-22*
