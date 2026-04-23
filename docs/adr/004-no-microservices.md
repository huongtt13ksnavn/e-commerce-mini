# ADR-004: No Microservices

## Status

Accepted

## Context

The e-commerce domain (catalog, auth, cart, orders) maps naturally onto four bounded contexts, which is exactly the decomposition a microservices architecture would produce. The question of "why not microservices" is a legitimate one for this domain and is worth answering explicitly for any senior engineer evaluating this codebase.

Microservices offer genuine benefits at scale: independent deployability, independent scalability per bounded context, technology heterogeneity, and fault isolation. They are the right choice when teams are large enough that independent deployment is more valuable than the overhead of distributed systems.

The costs of microservices include: distributed transactions (no single database transaction spans a service boundary — cart checkout touching both Cart and Order services requires a saga or 2PC), inter-service authentication, service discovery, distributed tracing, a service mesh or API gateway, and a CI/CD pipeline per service. These costs are fixed regardless of team size.

## Decision

Deploy as a single monolith. One Docker container for the API, one for PostgreSQL. `docker compose up` is the complete deployment.

The internal architecture (Clean Architecture with bounded domains in `ECommerce.Domain`) preserves the logical separation of Cart, Order, and Product without paying the distributed systems tax. If the system needed to scale to multiple teams or services in the future, the bounded contexts are already delineated — extraction would be a packaging decision, not a redesign.

## Consequences

**Positive:**
- `docker compose up` is the entire deployment. Zero manual steps. Zero infrastructure configuration. Any engineer with Docker Desktop can run the full system in under 90 seconds.
- Cart checkout and order placement are a single database transaction. No distributed transaction strategy needed. No saga. No eventual consistency on the write path.
- CI is a single build and test job. No per-service pipelines to maintain.
- Integration tests use a single `WebApplicationFactory` with Testcontainers — one real PostgreSQL instance, no service stubs.

**Negative:**
- The entire application deploys as one unit. A change to the product catalog requires redeploying the entire application.
- A single database means schema migrations affect all bounded contexts simultaneously. At high team count, this creates coordination overhead.
- If any bounded context needs a different scaling profile (e.g., the catalog needs read replicas), the entire application must be replicated rather than just the relevant service.

These are known, accepted constraints for the current scope: single developer, portfolio project, 5-day build.
