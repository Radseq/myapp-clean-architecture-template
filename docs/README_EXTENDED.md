# MyApp — Modular Monolith Clean Architecture Template (Extended)

## 1) Goals

- Modular monolith that can be **mechanically split into microservices**
- Clean Architecture per module (Domain / Application / Infrastructure / Presentation)
- CQRS (MediatR) + FluentValidation
- Explicit result model (`MessageResult`, `ErrorData`) instead of exceptions as business flow
- Centralized transaction boundary (UoW behavior) + explicit exception pattern
- Outbox for reliable integration + retry/backoff
- Observability-first (CorrelationId, structured logs, request logging, optional body capture)

---

## 2) Solution structure

```text
src/
  BuildingBlocks/
    MyApp.BuildingBlocks.Domain
    MyApp.BuildingBlocks.Application
    MyApp.BuildingBlocks.Infrastructure
    MyApp.BuildingBlocks.Presentation

  IntegrationContracts/
    MyApp.IntegrationContracts
      Outbox/
      Transport/Commands

  Modules/
    Orders/
      MyApp.Modules.Orders
      MyApp.Modules.Orders.Domain
      MyApp.Modules.Orders.Application
      MyApp.Modules.Orders.Infrastructure
      MyApp.Modules.Orders.Presentation
      MyApp.Modules.Orders.Contracts

    Transport/
      MyApp.Modules.Transport
      MyApp.Modules.Transport.Application
      MyApp.Modules.Transport.Infrastructure

  MyApp.Host/   # the only entrypoint (composition root)
```

**Rule:** modules do not reference each other for business logic. Cross-module communication happens through:
- Integration contracts (versioned message contracts)
- Outbox (eventual consistency)
- Optional synchronous HTTP only when justified

---

## 3) Quick start

### Prerequisites
- .NET SDK (recommended: .NET 8+)
- SQL Server
- Redis (optional; caching and/or failed payload store)

### Run
```bash
dotnet restore
dotnet run --project src/MyApp.Host
```

---

## 4) Configuration (appsettings.json)

Recommended skeleton (adjust for your env):

```json
{
  "ConnectionStrings": {
    "OrdersDb": "Server=(local)\\SQLEXPRESS;Database=MyApp;Trusted_Connection=True;TrustServerCertificate=True;",
    "Default":  "Server=(local)\\SQLEXPRESS;Database=MyApp;Trusted_Connection=True;TrustServerCertificate=True;",
    "Redis": "localhost:6379"
  },

  "Caching": {
    "UseRedis": false,
    "KeyPrefix": "myapp"
  },

  "TransportApi": {
    "BaseUrl": "https://transport.example.com/"
  },

  "Outbox": {
    "Orders": {
      "BatchSize": 20,
      "PollInterval": "00:00:02",
      "LeaseTime": "00:00:30",
      "MaxAttempts": 20,
      "MinBackoff": "00:00:02",
      "MaxBackoff": "00:10:00"
    }
  },

  "Observability": {
    "Logging": {
      "UseNLog": true,
      "NLogConfigFile": "nlog.config",
      "DiagnosticsEnabled": false
    },

    "RequestLogging": {
      "LogQueryString": false,
      "LogHeaders": false,
      "MaxValueLength": 256,
      "HeaderAllowList": [ "User-Agent", "Content-Type", "Accept-Language" ],
      "HeaderDenyList": [ "Authorization", "Cookie", "Set-Cookie" ],
      "QueryStringDenyList": [ "password", "token", "access_token", "refresh_token" ]
    },

    "BodyLogging": {
      "Enabled": false,
      "Mode": 2,
      "MaxBytes": 4096,
      "MaxRequestContentLengthToCapture": 32768,
      "AllowUnknownContentLength": false,
      "ContentTypesAllowList": [
        "application/json",
        "application/problem+json",
        "text/plain"
      ],
      "JsonDenyPaths": [
        "password",
        "token",
        "access_token",
        "refresh_token",
        "client_secret"
      ],
      "Store": {
        "Mode": "Redis",
        "TtlMinutes": 60,
        "KeyPrefix": "failed-http"
      }
    }
  }
}
```

---

## 5) Request flow (real execution order)

### HTTP pipeline registration order

Request goes top→down; response/exception unwinds bottom→up.

1. `CorrelationIdMiddleware`
2. `RequestLoggingMiddleware`
3. `BodyOnErrorLoggingMiddleware`
4. `GlobalExceptionMiddleware`
5. Controllers

**Why this order**
- exception middleware must be inside the pipeline to generate `application/problem+json`
- body capture must wrap around it to capture response body produced by exception handler
- request logging should measure final status + latency for every request

### MediatR pipeline order (outer → inner)

```text
RequestLoggingBehavior
↓
ValidationBehavior
↓
QueryCachingBehavior (queries only)
↓
UnitOfWorkBehavior (commands only)
↓
Handler
```

---

## 6) Result & error contract

### `MessageResult` / `MessageResult<T>`

- Success: `Ok()`, `Ok(value)`
- Partial success: `Partial(warnings)` or `Ok(value, warnings)` (implementation detail)
- Failure: `Fail(errors)` / `Fail(error)`

### `ErrorData`

Stable error contract:
- `Code` (stable numeric id)
- `Key` (localization key)
- `Args` (format args)
- `Description` (fallback)
- `Kind` (internal mapping, not necessarily exposed)

### Localization order
1) RESX by Key  
2) Description  
3) Key  

### HTTP status mapping

Implemented mapping is based on dominant `ErrorKind` (priority: Unexpected > Dependency > Auth > Conflict > NotFound > Validation):
- Validation → 400
- NotFound → 404
- Conflict → 409
- Unauthorized → 401
- Forbidden → 403
- DependencyFailure → 502
- DependencyTimeout → 504
- Unexpected → 500

---

## 7) Unit of Work and transactions

### Default rule (template contract)
Handlers:
- do not call `SaveChanges`
- do not open transactions manually

### Exception rule (supported by template)

If a use-case **must obtain DB-generated identity** within the operation (e.g., `order.Id` needed for response and/or outbox payload), the request can implement:

- `ISkipUnitOfWorkBehavior`

Then the handler owns:
- `ExecutionStrategy().ExecuteAsync(...)`
- explicit `BeginTransactionAsync`
- `SaveChanges #1` (generate identity)
- outbox insert
- `SaveChanges #2`
- commit

This is a controlled, documented escape hatch.

---

## 8) Outbox pattern (integration backbone)

### What it solves
- no lost messages during crashes/retries
- reliable eventual consistency for downstream systems
- repeatable retry + backoff without “double-dispatch”

### Ownership model
- Orders **owns** outbox storage and worker
- Transport **consumes** Orders outbox messages

### Dispatching
- Each message has:
  - `Type` (versioned)
  - `PayloadJson`
  - `IdempotencyKey`
  - `CorrelationId`
  - attempt counters + retry schedule
- Outbox worker:
  - acquires batch (locks rows)
  - dispatches to handler by message Type
  - applies result (Done/Retry/Dead) and schedules next attempt

### Hot dispatch (best-effort)
For “create and dispatch” UX, after DB commit the API can try `TryDispatchOnceAsync(outboxId)`:
- if success → return `TransportStatus=Sent`
- else → return `TransportStatus=Queued` (worker will deliver later)

---

## 9) Idempotency (what is implemented vs recommended)

### Implemented
- Outbound call to Transport API sends `Idempotency-Key` header (based on `ExternalCorrelationId`).
- Outbox envelope stores `IdempotencyKey` per message.

### Recommended (not fully implemented as a generic feature)
- **Inbound idempotency store** for create endpoints:
  - store client-provided idempotency key + response fingerprint
  - on repeat → return stored response
This prevents duplicate Orders at API boundary.

---

## 10) Retry strategy (recommended)

### Recommended (not wired as a Polly package by default)
For outbound HttpClient:
- Retry: transient failures, 5xx, 429, timeouts
- Circuit breaker: protect your system during downstream outage
- Timeout: hard per-call timeout

The template already supports reliability at the integration boundary via Outbox; Polly is usually for *direct synchronous calls*.

---

## 11) Caching (optional)

Implemented:
- `QueryCachingBehavior` for cacheable queries
- cache backends: Memory or Redis (depending on `Caching:UseRedis`)
- cache failures degrade to “no-cache” (no request failure)

Recommended:
- TTL conventions per query
- “negative caching” (not-found TTL) only if safe for your domain

---

## 12) Observability

### CorrelationId
Header: `X-Correlation-ID`

- if provided → propagated (sanitized)
- if missing → generated
- returned in response
- propagated to outbound HttpClient via `CorrelationIdDelegatingHandler`

### Log taxonomy (practical)
- HTTP access summary: `RequestLoggingMiddleware` (EventId=1001)
- Unhandled exceptions: `GlobalExceptionMiddleware` (EventId=1002)
- Payload captured (optional): `BodyOnErrorLoggingMiddleware` (EventId=1003)
- Application outcome: `RequestLoggingBehavior` (logs OK/PARTIAL/FAILED)

### Body capture (optional)
`BodyOnErrorLoggingMiddleware`:
- gated by allow-list of content types + size limits
- policy per request: Default/Force/Suppress
- store backend: None | Redis | Sql
- TTL

Default is OFF (production-safe).

### Diagnostics endpoint
When enabled:
- `GET /health/logging`

---

## 13) Adding a new use-case (pattern)

### Query
```csharp
public sealed record GetOrderById(int Id) : IQuery<OrderDto>;
```

### Command
```csharp
public sealed record CreateOrder(...) : ICommand<CreateOrderResponse>;
```

### Transactional command (full atomic write)
```csharp
public sealed record CreateOrder(...) : ITransactionalCommand<CreateOrderResponse>;
```

### Validator
```csharp
sealed class CreateOrderValidator : AbstractValidator<CreateOrder> { }
```

---

## 14) Production readiness checklist

### Implemented (in this codebase)
- [x] CorrelationId end-to-end (inbound + outbound)
- [x] Structured logging with scopes + event ids
- [x] Localized errors (RESX + fallback)
- [x] Stable error codes/keys
- [x] ProblemDetails boundary
- [x] HTTP request logging with redaction options
- [x] Optional body capture (Redis/Sql/None) + TTL
- [x] Query caching (Memory/Redis)
- [x] Outbox + retry/backoff + dead-lettering semantics

### Recommended / optional (may be added per project)
- [ ] Inbound idempotency store for create endpoints
- [ ] Rate limiting
- [ ] Health checks (database, redis, downstream)
- [ ] OpenTelemetry exporter wiring (traces/metrics)
- [ ] Central log aggregation (ELK/Seq/Loki)
- [ ] Security hardening (CORS, headers, input size limits)
- [ ] Background job host split (if needed)

---

## 15) Migration to microservices (mechanical split)

When extracting a module into a separate service:
1. Move module projects into new service repo
2. Keep `IntegrationContracts` as shared NuGet
3. Assign module its own DB
4. Keep Outbox in the owning service
5. Replace in-process integration with HTTP/message broker
6. Remove module from monolith host

Because modules are already isolated and integration is contract-based, the split is mostly composition work.

---

## License
Choose per your needs (MIT/Apache-2.0/internal).
