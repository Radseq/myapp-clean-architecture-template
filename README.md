# MyApp — Modular Monolith Clean Architecture Template

Production-ready ASP.NET Core template built as a **Modular Monolith** with a **clean migration path to microservices**.

This project demonstrates a real-world architecture used in production systems:

- Clean Architecture per module
- CQRS with MediatR
- FluentValidation
- Explicit Result Pattern (`MessageResult`)
- Outbox Pattern for reliable integrations
- Observability-first logging
- Transaction boundaries via MediatR pipeline

---

## Table of Contents

### Getting Started
- [Quick Start](#quick-start)
- [Configuration](#configuration)

### Architecture
- [Project Structure](#project-structure)
- [Architecture diagram (C4)](#architecture-diagram-c4)
- [Request lifecycle](#request-lifecycle)
- [HTTP Pipeline](#http-pipeline)
- [MediatR Pipeline](#mediatr-pipeline)
- [Unit of Work](#unit-of-work)
- [Result Pattern](#result-pattern)

### Integrations
- [Outbox Pattern](#outbox-pattern)
- [Sequence diagram: Outbox flow](#sequence-diagram-outbox-flow)
- [Transport Integration](#transport-integration)
- [Idempotency](#idempotency)

### Observability
- [CorrelationId](#correlationid)
- [Request Logging](#request-logging)
- [Body Capture](#body-capture)

### Development
- [Adding a Module](#adding-a-module)
- [Adding a Use Case](#adding-a-use-case)

### Deployment & Scaling
- [Production Checklist](#production-checklist)
- [Migration to Microservices](#migration-to-microservices)

### Full Documentation
- [docs/README_EXTENDED.md](docs/README_EXTENDED.md)

---

## Quick Start

### Requirements

- .NET 8+
- SQL Server
- Redis (optional)

### Run

```bash
dotnet restore
dotnet run --project src/MyApp.Host
```

---

## Configuration

Important configuration sections:

| Section | Purpose |
|------|------|
| ConnectionStrings:OrdersDb | Orders module database |
| ConnectionStrings:Redis | Redis cache / body store |
| Outbox:Orders | Outbox worker configuration |
| Observability | Logging & request capture |

See a full configuration example and detailed notes in **docs/README_EXTENDED.md**.

---

## Project Structure

```text
src/
  BuildingBlocks/
  IntegrationContracts/
  Modules/
  MyApp.Host/
docs/
  README_EXTENDED.md
```

Modules follow **Clean Architecture**:

```text
Domain
Application
Infrastructure
Presentation
```

Modules **do not reference each other directly**. Integration happens through:

- Outbox
- Integration contracts
- HTTP (optional)

---

## Architecture diagram (C4)

Pasteable **Mermaid** C4-style diagram for README.

```mermaid
flowchart TB
  user["Client / UI"] --> host["MyApp.Host (ASP.NET Core)"]
  host --> pres["Presentation (Controllers + Middlewares)"]
  pres --> app["Application (MediatR Handlers + Behaviors)"]
  app --> dom["Domain (Aggregates, Policies)"]
  app --> infra["Infrastructure (EF, HttpClients, Outbox)"]
  infra --> db["SQL Server"]
  infra --> redis["Redis (optional)"]
  infra --> ext["External Transport API"]

  subgraph Modules
    orders["Orders Module"]
    transport["Transport Module"]
  end

  host --> orders
  host --> transport
```

---

## Request lifecycle

```mermaid
sequenceDiagram
  autonumber
  participant C as Client
  participant API as ASP.NET Core (Host)
  participant M as Middleware Pipeline
  participant CTR as Controller
  participant MR as MediatR Pipeline
  participant H as Handler
  participant UoW as UnitOfWorkBehavior
  participant DB as SQL Server

  C->>API: HTTP request
  API->>M: CorrelationId + RequestLogging + (BodyOnError) + Exception
  M->>CTR: Route to controller/action
  CTR->>MR: Send Command/Query
  MR->>H: Validate/Caching/UoW
  alt Query
    H->>DB: Read
    DB-->>H: Data
    H-->>MR: Result
  else Command
    MR->>UoW: Begin tx + SaveChanges on success
    UoW->>H: Execute handler
    H->>DB: Write
    DB-->>H: OK
    H-->>UoW: Result
    UoW->>DB: Commit
    UoW-->>MR: Result
  end
  MR-->>CTR: Result
  CTR-->>M: HTTP response (ProblemDetails on fail)
  M-->>C: Response + X-Correlation-ID
```

---

## HTTP Pipeline

Order of middleware:

```text
CorrelationIdMiddleware
RequestLoggingMiddleware
BodyOnErrorLoggingMiddleware
GlobalExceptionMiddleware
Controllers
```

---

## MediatR Pipeline

Order of behaviors:

```text
RequestLoggingBehavior
ValidationBehavior
QueryCachingBehavior (queries)
UnitOfWorkBehavior (commands)
Handler
```

---

## Unit of Work

Default rule:

Handlers **must not**:
- open transactions
- call `SaveChanges`

Transaction boundaries are controlled by **UnitOfWorkBehavior**.

### Exception hatch

If a use case needs **database identity during execution** (e.g., `order.Id`):

- request implements `ISkipUnitOfWorkBehavior`
- handler owns: `ExecutionStrategy` + explicit transaction + `SaveChanges` steps

Used in: `CreateOrderAndDispatchTransport`.

---

## Result Pattern

Handlers return:

- `MessageResult`
- `MessageResult<T>`

Error contract:

- `ErrorData` (Code, Key, Args, Description)

Errors are translated to **ProblemDetails** at API boundary.

---

## Outbox Pattern

Integration backbone of the system.

Ensures:
- reliable integration
- retry on failures
- no lost messages

### Sequence diagram: Outbox flow

```mermaid
sequenceDiagram
  autonumber
  participant API as Orders API
  participant DB as OrdersDb (SQL)
  participant OB as Outbox Worker
  participant H as Outbox Handler (Transport)
  participant T as External Transport API

  API->>DB: Begin tx
  API->>DB: Insert Order
  API->>DB: Insert OutboxMessage(type=TransportOrderCreated)
  API->>DB: Commit tx

  loop Poll batch
    OB->>DB: Lease N pending messages
    DB-->>OB: Messages
    OB->>H: Dispatch message
    H->>T: POST /transport (Idempotency-Key)
    alt success
      T-->>H: 2xx
      H-->>OB: Success
      OB->>DB: Mark Done
    else transient failure
      T-->>H: 5xx/timeout
      H-->>OB: Retry(after backoff)
      OB->>DB: Schedule NextAttemptUtc
    else permanent failure
      T-->>H: 4xx validation
      H-->>OB: Dead-letter
      OB->>DB: Mark Dead
    end
  end
```

---

## Transport Integration

Transport module consumes Orders outbox and calls external Transport API.

Outbound request includes:

- `Idempotency-Key` header to prevent duplicate side effects
- `X-Correlation-ID` (propagated)

---

## Idempotency

Implemented:
- idempotent outbound calls
- idempotency key stored in outbox envelope

Recommended (optional):
- inbound idempotency store for create endpoints

---

## Observability

System includes:

| Feature | Description |
|---|---|
| CorrelationId | request tracing |
| RequestLogging | HTTP summary logs |
| Body capture | request/response capture (optional) |
| Structured logs | JSON logs + scopes |

---

## CorrelationId

Header:
- `X-Correlation-ID`

Behavior:
- propagate if provided
- generate if missing
- return in response
- attach to logs
- propagate to outbound HTTP

---

## Request Logging

Logs:
- method
- path
- status
- latency

Optional:
- headers
- query string

Sensitive values are redacted.

---

## Body Capture

`BodyOnErrorLoggingMiddleware`

Captures payloads only when needed.

Supports:
- Redis store
- SQL store
- TTL expiration

Default: **disabled**.

---

## Adding a Module

1) Create: `src/Modules/<Module>`
2) Create projects: Domain, Application, Infrastructure, Presentation, Contracts
3) Add assembly markers per layer
4) Register module in host: `services.Add<Module>Module(...)`
5) Map UoW routing for module’s Application assembly
6) Define integration contracts if needed

---

## Adding a Use Case

### Query
```csharp
public sealed record GetOrder(int Id) : IQuery<OrderDto>;
```

### Command
```csharp
public sealed record CreateOrder(...) : ICommand<CreateOrderResponse>;
```

### Validator
```csharp
public sealed class CreateOrderValidator : AbstractValidator<CreateOrder> { }
```

---

## Production Checklist

Implemented:
- CorrelationId propagation (inbound + outbound)
- Structured logging
- ProblemDetails boundary
- Error localization (RESX)
- Outbox retry/backoff
- Query caching (Memory/Redis)
- Optional payload store (Redis/Sql) with TTL

Recommended:
- rate limiting
- health checks (db/redis/downstream)
- OpenTelemetry exporter wiring (traces/metrics)
- centralized log aggregation
- inbound idempotency store

---

## Migration to Microservices

Because modules are isolated, extraction is mechanical:

1) Move module to new service repo
2) Give it its own DB
3) Keep IntegrationContracts as shared NuGet
4) Keep Outbox in owning service
5) Replace in-process integration with HTTP/message broker
6) Remove module from monolith host

---

## Full Documentation

Deep dive:

- docs/README_EXTENDED.md
