# MyApp API

> Production-ready ASP.NET Core API based on Clean Architecture, CQRS (MediatR), explicit result modeling (MessageResult), structured error contracts, and full observability (CorrelationId + ProblemDetails).

---

# 1️⃣ Architecture Overview

## High-Level Architecture

```
Client
   │
   ▼
[ ASP.NET Core API ]
   │   (Middlewares)
   ▼
[ MediatR Pipeline ]
   │   (Behaviors)
   ▼
[ Application Handlers ]
   │
   ▼
[ Infrastructure ]
   │
   ▼
[ Database / External APIs ]
```

---

## Layers

### Domain
- `MessageResult`
- `MessageResult<T>`
- `ErrorData`
- Domain Errors catalog (`Errors.*`)

### Application
- `ICommand`, `IQuery`
- `ITransactionalCommand`
- Handlers
- FluentValidation validators
- MediatR pipeline behaviors

### Infrastructure
- `EfUnitOfWork`
- Repositories
- AutoMapper profiles
- DB exception mapping
- Outbox (after create order periodic send request to http transport if failed)
- Save to db request with results when app return error
- Caching in memory (or redis) http get

### API
- Middlewares
- HTTP result mapping (ApiResponse / ProblemDetails)
- CorrelationId propagation
- Versioning helpers

---

# 2️⃣ Request Flow (Real Execution Order)

## HTTP Pipeline Order

1. `CorrelationIdMiddleware`
2. `RequestLoggingMiddleware`
3. `GlobalExceptionMiddleware`
4. Controller
5. MediatR.Send(...)
6. Pipeline behaviors (in order registered)

---

## MediatR Pipeline Order

Recommended order:

```
RequestLoggingBehavior
↓
ValidationBehavior
↓
QueryCachingBehavior
↓
UnitOfWorkBehavior
↓
Handler
```

---

## Detailed Flow Example (Command)

```
HTTP POST /orders
  ↓
CorrelationId assigned
  ↓
Controller → SendOk(...)
  ↓
RequestLoggingBehavior
  ↓
ValidationBehavior
  → validation fail? → MessageResult.Fail(...)
  ↓
UnitOfWorkBehavior
  → begin transaction? (if ITransactionalCommand)
  ↓
Handler
  → returns MessageResult
  ↓
UnitOfWorkBehavior
  → Success? SaveChanges
  → Partial? SaveChanges
  → Failure? rollback / skip SaveChanges
  ↓
HTTP Mapping
  → Success → 200/201 ApiResponse
  → Partial → 200 + warnings
  → Failure → ProblemDetails
```

---

# 3️⃣ MessageResult Contract

## Why MessageResult Exists

- No exceptions as business flow
- Explicit Partial Success support
- Predictable mapping to HTTP
- Stable error structure for clients

---

## States

### Success
```
MessageResult.Ok()
```

### Partial
```
MessageResult.Partial(warnings)
```

Used when:
- DB commit succeeded
- Downstream integration failed
- Non-critical step failed

---

### Failure
```
MessageResult.Fail(errors)
```

Used for:
- Validation errors
- Not found
- Domain violations
- DB constraint errors

---

# 4️⃣ Error Contract

## ErrorData Structure

- `Code` → stable numeric code
- `Key` → localization key
- `Args` → formatting parameters
- `Description` → fallback text
- `ExtendedErrors` → nested field errors

---

## Localization Resolution Order

1. RESX by `Key`
2. `Description`
3. `Key`

---

## HTTP Status Mapping Rules

Priority:
1. Explicit mapping by `Code`
2. Explicit mapping by `Key`
3. Heuristics

Examples:
- `*.not_found` → 404
- `*.validation*` → 400
- `duplicate/foreign_key` → 409
- `transport.api_failed` → 502
- `unexpected` → 500

---

# 5️⃣ Unit of Work Behavior

## Core Principles

- Handlers DO NOT call `SaveChanges`
- Handlers DO NOT open transactions manually
- Retry logic must not be broken by manual transaction management

---

## EfUnitOfWork Features

- Nested transaction scope
- Rollback requested flag
- Post-save action queue
- DB exception mapping:
  - Unique constraint
  - FK constraint
  - Concurrency conflict
  - Unexpected

---

## Golden Rule

> Transaction boundary is controlled by pipeline, not handler.

---

# 6️⃣ Observability

## CorrelationId

Header:
```
X-Correlation-ID
```

Behavior:
- If provided → propagated
- If missing → generated
- Always returned in response

Injected into:
- Logger scope
- Activity (OpenTelemetry ready)
- Downstream HttpClient

---

## Middlewares

### CorrelationIdMiddleware
- Adds correlation id
- Adds logging scope

### RequestLoggingMiddleware
Logs:
```
{method} {path} => {status} in {ms}
```

### GlobalExceptionMiddleware
- Converts unhandled exceptions to ProblemDetails
- DEV mode includes extra exception info
- `OperationCanceledException` → 499

---

# 7️⃣ HTTP Response Contract

## Success

```
200 OK
{
  "value": {...},
  "warnings": []
}
```

---

## Partial Success

```
200 OK
{
  "value": {...},
  "warnings": [ ... ]
}
```

---

## Failure

```
4xx / 5xx
ProblemDetails
```

Includes:
- `errors`
- `warnings` (if present)
- `traceId`
- `correlationId`

---

# 8️⃣ Integration Patterns

## Pattern 1 — Sync Integration (Simple)

Handler:

```
Save DB
Call external API
If API fails → Partial
```

Risk:
- External API fails after DB commit

---

## Pattern 2 — Outbox (Recommended Production Pattern)

Flow:

```
Handler:
  - Save Order
  - Save OutboxMessage
Commit

Background Worker:
  - Read outbox
  - Send HTTP
  - Retry on transient
  - Mark as processed
```

Advantages:
- Safe retry
- No lost messages
- Clean separation
- Resilient architecture

---

# 9️⃣ Idempotency

## For Create Operations

Client sends:
```
Idempotency-Key: <GUID>
```

Server:
- Store key + result
- If repeated → return stored result

This prevents:
- Duplicate orders
- Double dispatch

---

# 🔟 Retry Strategy (HttpClient + Polly)

Recommended policies:

### Retry
- Transient HTTP failures
- Timeout
- 5xx
- 429

### Circuit Breaker
- Protect system during downstream outage

### Timeout
- Hard timeout per call

---

# 1️⃣1️⃣ Caching (Optional Layer)

If enabled:

```
Caching:UseRedis = true
```

Behavior:
- Query-level caching
- TTL per query
- Optional CacheNotFound TTL
- Global or scoped cache

---

# 1️⃣2️⃣ Production Readiness Checklist

## Mandatory

- [x] CorrelationId
- [x] Structured logging
- [x] Localized errors
- [x] Stable error codes
- [x] ProblemDetails compliance
- [x] DB exception mapping
- [x] Transaction boundary centralized
- [x] Partial success support

---

## Recommended

- [ ] Outbox pattern for integrations
- [ ] Idempotency store
- [ ] Polly retry/circuit breaker
- [ ] Rate limiting
- [ ] Health checks
- [ ] OpenTelemetry exporter
- [ ] Structured log aggregation (ELK / Seq / Loki)
- [ ] Security hardening (headers, CORS, input limits)

---

# 1️⃣3️⃣ Adding New Use Case

### Query
```
public sealed record GetOrderQuery(...) : IQuery<OrderDto>;
```

### Command
```
public sealed record CreateOrder(...) : ICommand<OrderDto>;
```

If needs transaction:
```
: ITransactionalCommand<OrderDto>
```

### Add Validator
```
class CreateOrderValidator : AbstractValidator<CreateOrder>
```

### Handler Returns
```
MessageResult.Ok(...)
MessageResult.Fail(...)
MessageResult.Partial(...)
```

---

# 1️⃣4️⃣ Architectural Principles Enforced

- ❌ No exceptions as control flow
- ❌ No SaveChanges in handlers
- ❌ No manual transaction in handlers
- ✅ Explicit result modeling
- ✅ Stable error contract
- ✅ Predictable HTTP mapping
- ✅ Observability-first design
- ✅ Integration resilience ready

---

# 1️⃣5️⃣ What Makes This “Senior-Level”

- Explicit result modeling
- Partial success support
- Error localization pipeline
- CorrelationId propagation end-to-end
- DB exception sanitization
- Transaction boundary enforcement
- Clear separation of concerns
- Integration safety patterns defined


Then adapt namespaces if needed.

## Run
1. Set connection string in `src/MyApp.Api/appsettings.json`
2. `dotnet restore`
3. `dotnet run --project src/MyApp.Api`