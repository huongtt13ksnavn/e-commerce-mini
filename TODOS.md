# TODOS

## Post-Day-5 Stretch Items

### OpenTelemetry traces
**What:** Add distributed tracing to the API using OpenTelemetry.
**Why:** The CEO plan's 10x vision calls out OTel traces as a key signal for "I understand production observability." Without it, the project demonstrates Clean Architecture but not production-readiness. A hiring manager who has been on-call knows the difference.
**Packages:**
```
OpenTelemetry.Extensions.Hosting
OpenTelemetry.Instrumentation.AspNetCore
OpenTelemetry.Instrumentation.EntityFrameworkCore
OpenTelemetry.Exporter.Console (for local dev)
```
**Wiring (Program.cs):**
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter());
```
**Effort:** ~1 hour with CC. Zero behavior change — purely additive.
**Depends on:** Day 5 complete (CI green + Docker Compose verified).
**Blog post angle:** "Adding OTel to a .NET 10 API in 60 minutes — what traces tell you that logs don't."
