# Changelog

All notable changes to this project will be documented in this file.

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
