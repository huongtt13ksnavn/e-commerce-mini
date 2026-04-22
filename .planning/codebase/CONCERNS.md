# Concerns & Technical Debt
_Last updated: 2026-04-22_

## Summary

This is a v0.2.0 early-stage project (2 days old) with solid architectural bones but almost zero real test coverage, a completely placeholder test file, and several domain aggregates (Cart, Order) implied by existing exception classes that have not been built yet. Secrets committed in plaintext config files and a hardcoded seeded admin credential represent the most actionable security issues. The codebase is clean and low on accumulated debt, but the gap between what is scaffolded and what is production-ready is significant.

---

## Technical Debt

### HIGH — Test suite is a placeholder stub
- **File:** `tests/ECommerce.IntegrationTests/UnitTest1.cs`
- The only test file is an auto-generated stub with an empty `Test1()` method body. It passes vacuously. CI runs `dotnet test` and reports green against zero assertions.
- The project has `Testcontainers.PostgreSql`, `Microsoft.AspNetCore.Mvc.Testing`, and `FluentAssertions` installed and ready — none are wired up.
- **Fix:** Replace `UnitTest1.cs` with a `WebApplicationFactory`-based fixture using Testcontainers to spin a real Postgres container. Write happy-path + error-path tests for at minimum: register, login, product CRUD, auth enforcement on admin endpoints.

### HIGH — Hardcoded admin seed credentials committed to source
- **File:** `src/ECommerce.Infrastructure/DependencyInjection.cs` lines 90–98
- `admin@example.com` / `Admin123!` is inserted on every cold-start if the user does not exist. These credentials are in version control and therefore in any clone, Docker image layer, and CI log.
- **Fix:** Source admin seed credentials from environment variables (`SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`) with a fallback only for local dev. Document clearly that the defaults must not be used in any non-local environment.

### HIGH — Plaintext JWT secret in committed config files
- **Files:** `src/ECommerce.API/appsettings.json` (placeholder `CHANGE_ME_TO_A_STRONG_SECRET_AT_LEAST_32_CHARS`), `src/ECommerce.API/appsettings.Development.json` (`dev-secret-at-least-32-characters-long!`), `docker-compose.yml` (`docker-compose-secret-at-least-32-chars!`)
- Secrets committed to source are effectively public. The `appsettings.Development.json` secret is functional — it will authenticate tokens in any environment that uses the Development profile.
- **Fix:** Remove all secret values from committed files. Use a `CHANGE_ME` placeholder only in `appsettings.json`. Source real values from environment variables or a secrets manager (e.g., `dotnet user-secrets` for dev, environment injection in CI/prod). Add `.gitignore` rules for `appsettings.*.json` overrides that contain real values.

### HIGH — Postgres credentials committed in docker-compose.yml and appsettings
- **Files:** `docker-compose.yml`, `src/ECommerce.API/appsettings.json`, `src/ECommerce.API/appsettings.Development.json`
- `Username=postgres;Password=postgres` appears in all three files committed to source.
- **Fix:** Same pattern as JWT secret — environment variable injection for any real deployment.

### MED — No unit tests for domain logic
- **Files:** `src/ECommerce.Domain/Entities/Product.cs`, `src/ECommerce.Domain/ValueObjects/Money.cs`
- Domain rules (e.g., `Update` throws when `!IsActive`, `Money.Of` rejects negative amounts, `Money.Add` rejects currency mismatch) are untested. These are the cheapest tests to write (no infrastructure) and the highest leverage for catching regressions.
- **Fix:** Add an `ECommerce.UnitTests` project and cover all domain aggregate methods and value object invariants.

### MED — `GetCurrentUserQuery` is a pass-through with no database verification
- **File:** `src/ECommerce.Application/Auth/Queries/GetCurrentUser/GetCurrentUserQueryHandler.cs`
- The `/api/auth/me` endpoint returns whatever claims are in the JWT token without ever hitting the database. A deleted, locked, or role-changed user will receive stale data from their unexpired token for up to 1 hour.
- **Fix:** Fetch the user from the database in the handler and verify they still exist and are not locked out. The JWT serves only as authentication, not as the source of truth for current user state.

### MED — JWT tokens are not revocable
- **Files:** `src/ECommerce.Infrastructure/Auth/JwtTokenGenerator.cs`, `src/ECommerce.Application/Auth/Queries/LoginUser/LoginUserQueryHandler.cs`
- Tokens have a 1-hour TTL with no refresh token mechanism and no server-side revocation list. There is no logout endpoint. An attacker who steals a token has access for up to 1 hour regardless of any server-side action.
- **Fix:** Implement refresh tokens (short-lived access + longer-lived refresh stored server-side) or add a token revocation store keyed on `jti` claim.

### MED — `GetProducts` loads all rows into memory with no pagination
- **Files:** `src/ECommerce.Infrastructure/Persistence/Repositories/ProductRepository.cs`, `src/ECommerce.Application/Products/Queries/GetProducts/GetProductsQueryHandler.cs`
- `GetAllAsync` does `WHERE IsActive = true` with no `LIMIT`/`OFFSET`. The in-memory `.Select()` projection also happens after loading all entities.
- **Fix:** Add `page` / `pageSize` parameters to `GetProductsQuery`, push `Skip`/`Take` to the database query, and return a paginated envelope (`items`, `totalCount`, `page`, `pageSize`).

### MED — `LoggingBehavior` does not log on exception
- **File:** `src/ECommerce.Application/Behaviors/LoggingBehavior.cs`
- The timing log only fires on success (after `await next()`). If the handler throws, no elapsed time is recorded. The separate `ExceptionHandlingBehavior` logs the error, but there is no correlation between "this request started" and "this request failed after Xms."
- **Fix:** Wrap `next()` in a try/finally in `LoggingBehavior` so elapsed time is always recorded, including on failure paths.

### LOW — `HealthCheckOptions` maps `Degraded` → 200
- **File:** `src/ECommerce.API/Endpoints/HealthEndpoints.cs`
- `HealthStatus.Degraded` returns HTTP 200 rather than a 2xx/degraded status. Kubernetes liveness probes and uptime monitors cannot distinguish healthy from degraded by HTTP status code alone.
- **Fix:** Map `Degraded` to 200 intentionally if degraded-but-alive is desired behavior, but document it. Alternatively map to 503 alongside Unhealthy if degraded means traffic should be shed.

### LOW — `AspNetCore.HealthChecks.NpgSql` version mismatch
- **File:** `src/ECommerce.Infrastructure/ECommerce.Infrastructure.csproj`
- `AspNetCore.HealthChecks.NpgSql` is pinned to `9.0.0` while all other ASP.NET Core packages target `10.*`. This is a deliberate or accidental version skip.
- **Fix:** Verify whether a `10.*`-compatible release exists and upgrade to it to keep the dependency graph coherent.

### LOW — `Money` currency defaults silently to USD
- **File:** `src/ECommerce.Domain/ValueObjects/Money.cs`
- `Money.Of(decimal amount)` defaults to `"USD"` with no explicit indication to callers. Every `CreateProduct` and `UpdateProduct` command silently stores USD regardless of user intent. There is no currency field in the API request DTOs.
- **Fix:** Either remove the default and require currency explicitly, or expose a currency field in `CreateProductRequest`/`UpdateProductRequest` and pass it through the command.

### LOW — `Product.Update` cannot clear `ImageUrl`
- **File:** `src/ECommerce.Domain/Entities/Product.cs` line 48
- `if (imageUrl is not null) ImageUrl = imageUrl;` means passing `null` in a PUT request silently ignores the field. A caller wanting to remove an image cannot do so.
- **Fix:** Use a discriminated union or explicit sentinel (e.g., empty string) to distinguish "don't change" from "clear the value." Alternatively change PUT semantics to always overwrite, including with null.

---

## Missing Pieces

### Cart aggregate (domain exception exists, entity does not)
- **File:** `src/ECommerce.Domain/Exceptions/CartEmptyException.cs`
- `CartEmptyException` is defined but there is no `Cart` entity, repository interface, CQRS handlers, or endpoints. The exception is dead code.
- The implied feature (add to cart, view cart, clear cart) is entirely absent.

### Order aggregate
- No `Order` entity, `IOrderRepository`, or order-related commands/queries exist. An e-commerce project without orders is a product catalogue, not a store.

### Refresh token / logout
- No `POST /api/auth/logout` endpoint. No refresh token flow. Sessions can only be ended by waiting for token expiry.

### User profile management
- No endpoints to update email, change password, or delete account. The `AppUser` entity has no profile fields beyond email.

### Pagination on `GET /api/products`
- The list endpoint returns all active products in a single response (see Technical Debt above). No pagination, sorting, or filtering is implemented.

### No `DeleteProduct` validator
- **Files:** `src/ECommerce.Application/Products/Commands/DeleteProduct/` — no `DeleteProductCommandValidator.cs` exists.
- The delete command only takes a GUID; validation is minimal, but consistency with other commands suggests a validator should exist.

### OpenTelemetry / distributed tracing
- **File:** `TODOS.md` — explicitly called out as a stretch item.
- No OTel instrumentation is wired. The project is not observable beyond structured console logs.

### No `HTTPS` redirect or HSTS in production
- **File:** `src/ECommerce.API/Program.cs`
- Neither `app.UseHttpsRedirection()` nor `app.UseHsts()` is registered. The Dockerfile exposes port 8080 (plain HTTP). For a production deployment behind a TLS-terminating proxy this may be acceptable, but it is not documented.

---

## Security Concerns

### Hardcoded admin seed credential (see Technical Debt — HIGH above)
- `admin@example.com` / `Admin123!` in `src/ECommerce.Infrastructure/DependencyInjection.cs`.

### JWT secret committed to source (see Technical Debt — HIGH above)
- `appsettings.Development.json` and `docker-compose.yml` both contain functional signing secrets.

### Postgres password committed to source (see Technical Debt — HIGH above)
- Appears in `docker-compose.yml` and `appsettings.json`/`appsettings.Development.json`.

### Rate limiter applies only to `/api/auth` routes
- **File:** `src/ECommerce.API/Program.cs`
- The `login` rate limit policy (5 req/min per IP) is applied to register and login. Admin write endpoints (`POST`, `PUT`, `DELETE /api/products`) and `GET /api/products` have no rate limiting. An authenticated Admin or unauthenticated bulk-reader can hammer the API without throttling.
- **Fix:** Add a broader rate limit policy for authenticated write operations and/or a global fallback policy.

### No CORS policy configured
- **File:** `src/ECommerce.API/Program.cs`
- No `AddCors` / `UseCors` registration. In development this may not matter, but any browser-based client (SPA, Scalar UI) will rely on default browser behavior. When a frontend is added, CORS will need explicit configuration.

### `UnauthorizedAccessException` is mapped to 401 in middleware
- **File:** `src/ECommerce.API/Middleware/ExceptionMiddleware.cs` line 43–46
- `UnauthorizedAccessException` (a BCL exception) is caught and returned as 401. This means any unrelated code path that throws this BCL exception (e.g., file system access) will accidentally return 401 to clients. A domain-specific `UnauthorizedException` should be used instead.

### `DbUpdateException` → 422 leaks schema hints
- **File:** `src/ECommerce.API/Middleware/ExceptionMiddleware.cs` line 36–39
- The 422 message says "A product with that name may already exist." This is an acceptable UX message but is hardcoded to the product uniqueness constraint. When other entities gain unique constraints, the same message will fire misleadingly.
- **Fix:** Inspect the inner exception (PostgreSQL error code `23505`) to generate a contextual message, or use a generic constraint message.

---

## Scalability Concerns

### `GetAllAsync` is unbounded (see Technical Debt — MED above)
- No pagination. As the product catalogue grows, `GET /api/products` will load the entire table into memory on every request.

### `MigrateAndSeedAsync` runs synchronously at startup on every pod
- **File:** `src/ECommerce.Infrastructure/DependencyInjection.cs` lines 56–77
- In a multi-replica deployment, all pods race to call `Database.MigrateAsync()` on startup. EF Core migrations are not concurrency-safe across multiple simultaneous callers. The Polly retry loop handles transient DB connection failures but not concurrent migration conflicts.
- **Fix:** Run migrations as a pre-deploy job (Kubernetes init container, CI step) rather than inline at application startup.

### Single-role model limits extensibility
- **File:** `src/ECommerce.Infrastructure/Auth/UserService.cs` line 37
- Roles are hardcoded to `"Admin"` or `"User"` with a binary check. A multi-role user (e.g., `Admin` + `Vendor`) would have role selection determined by presence of `"Admin"` only. Fine for current scope but will require rework for any RBAC expansion.

### Domain events are published after `SaveChangesAsync` with no outbox
- **File:** `src/ECommerce.Infrastructure/Persistence/AppDbContext.cs` lines 22–36
- Domain events are published in-memory after the DB transaction commits. If `publisher.Publish()` throws, the transaction has already been committed but the event was not handled. There is no outbox pattern, dead-letter queue, or retry mechanism for event dispatch.
- **Fix:** For simple internal use this is acceptable, but if domain events drive side effects (emails, inventory updates), implement a transactional outbox pattern.

---

## Incomplete Implementations

### `tests/ECommerce.IntegrationTests/UnitTest1.cs` — empty test body
- The sole test file has a single empty `[Fact]`. All test infrastructure packages are installed but unused. This is the most critical gap given CI runs this test and reports green.

### `src/ECommerce.Domain/Exceptions/CartEmptyException.cs` — dead code
- Exception class for a Cart feature that does not exist anywhere else in the codebase.

### `UserId` value object — unused by domain entities
- **File:** `src/ECommerce.Domain/ValueObjects/UserId.cs`
- `UserId` is defined but `AppUser.Id` (from ASP.NET Identity) is a raw `string`, and handlers use `Guid.Parse(user.Id)` directly. `UserId` is never used in the domain or application layers.
- **Fix:** Either adopt `UserId` consistently in auth flows, or remove it to avoid confusion.

### `TODOS.md` — OpenTelemetry tracing
- Explicitly documented as a stretch item. No OTel packages are referenced in any `.csproj`. Traces, metrics, and spans are absent.

### Scalar API reference exposed in Development only — no auth UI flow documented
- **File:** `src/ECommerce.API/Program.cs` lines 71–74
- Scalar is mounted only in `Development`. There is no documentation of how to configure Scalar with JWT Bearer auth for manual testing, which makes the development experience for secured endpoints awkward.
