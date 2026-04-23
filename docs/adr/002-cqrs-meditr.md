# ADR-002: CQRS via MediatR

## Status

Accepted

## Context

E-commerce applications have an inherently asymmetric read/write pattern. Reads — product listing, cart retrieval, order history — happen far more frequently than writes. Treating reads and writes identically (same validation pipeline, same repository abstraction) adds overhead where it is not needed and obscures where it is.

Cross-cutting concerns also needed to apply uniformly: every command needs input validation; every operation needs structured logging; every unhandled exception needs to map to a ProblemDetails response rather than a raw 500. Without a mediator, these concerns are either duplicated in each handler or implemented as ASP.NET middleware that cannot intercept application-layer failures.

The alternative considered was service classes (e.g., `IProductService`, `IOrderService`) with direct method calls from endpoints. This is the most common pattern in .NET codebases. It works, but cross-cutting concerns become scattered: each service method ends up with its own try/catch, its own logging call, its own validation invocation.

## Decision

Use MediatR 12 for command/query dispatch. All use cases are `IRequest<TResponse>` records. Commands mutate state; queries return data. No shared base class distinguishes them — convention only.

Three pipeline behaviors are registered in this order (outermost first):

1. `ExceptionHandlingBehavior<TRequest, TResponse>` — catches all unhandled exceptions and maps to ProblemDetails. `DomainException` → 400; `ValidationException` → 422; all others → 500.
2. `LoggingBehavior<TRequest, TResponse>` — logs request name and elapsed milliseconds.
3. `ValidationBehavior<TRequest, TResponse>` — runs all registered FluentValidation validators for the request type; throws `ValidationException` on failure.

Queries bypass `ValidationBehavior` naturally — no validator is registered for them, so the behavior is a no-op.

## Consequences

**Positive:**
- Pipeline behaviors compose cleanly. Adding a new cross-cutting concern (e.g., caching, idempotency) is one `IPipelineBehavior<TRequest, TResponse>` implementation and one DI registration.
- Commands and queries are self-documenting records. The full list of application use cases is the list of `IRequest` types — no service interface scanning needed.
- ExceptionHandling is guaranteed to run for every operation, so no handler can accidentally leak a raw exception to the HTTP response.

**Negative:**
- MediatR is runtime indirection. Stack traces show `MediatR.Mediator.Send` rather than the direct call site, which can confuse engineers unfamiliar with the library.
- Can feel like over-engineering for CRUD endpoints with no real invariants to protect. A `GetProductsQuery` that just calls `repository.GetAllAsync()` is two files (query + handler) where one method call would suffice.
- MediatR is a third-party dependency. If the library is abandoned or breaks API compatibility, all handlers are affected.
