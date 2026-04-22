# Changelog

All notable changes to this project will be documented in this file.

## [0.2.0] - 2026-04-22

### Added
- Product aggregate root with soft-delete (`IsActive` flag) and `Money` value object (amount + currency)
- Full Product CRUD via MediatR CQRS: `CreateProduct`, `UpdateProduct`, `DeleteProduct` commands; `GetProduct`, `GetProducts` queries
- Five REST endpoints — `GET /api/products` and `GET /api/products/{id}` are public; `POST`, `PUT`, `DELETE` require the `AdminOnly` JWT role
- FluentValidation for all product commands: name (max 200), description (max 2000), price (`> 0` — zero-price blocked), stock (`>= 0`), image URL (HTTPS only, max 500)
- EF Core configuration for Product: unique index on `Name`, PostgreSQL `xmin` system column as optimistic concurrency token (zero-DDL, row-version at query time)
- Two EF Core migrations: `AddProduct` (full table) and `AddProductConstraints` (unique name index; `xmin` requires no DDL)
- Database seeder — 10 tech products inserted on startup if the table is empty
- `DbUpdateConcurrencyException` → 409 Conflict and `DbUpdateException` → 422 Unprocessable Entity in the global exception middleware

## [0.1.0] - 2026-04-21

### Added
- Day 1 scaffold: 4-layer Clean Architecture (Domain, Application, Infrastructure, API)
- JWT Bearer authentication with register/login/me endpoints
- CQRS via MediatR 12 with ExceptionHandling → Logging → Validation pipeline behaviors
- EF Core 10 + PostgreSQL + ASP.NET Identity
- Per-IP rate limiting on `/api/auth/register` and `/api/auth/login` (5 req/min)
- Docker Compose and GitHub Actions CI
- FluentValidation with async validators

### Fixed (security/correctness review)
- Rate limiter changed from global bucket to per-IP partition (was trivially bypassable)
- `/api/auth/register` now rate-limited (was missing the policy)
- Password lockout enforced via `SignInManager.CheckPasswordSignInAsync` (was bypassing lockout)
- Domain events cleared before `SaveChangesAsync` to prevent double-dispatch on retry
- `RegistrationFailedException` thrown instead of leaking Identity errors as 500s
- FluentValidator non-alphanumeric rule added to match ASP.NET Identity password policy
- Async validators used in `ValidationBehavior` (was blocking threadpool with `.Result`)
- `Jwt:Secret` null-forgiving operator replaced with explicit startup guard
- `Guid.Parse` in `UserId.From()` replaced with `Guid.TryParse` + domain exception
