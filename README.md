# MyApp API

> Production-ready ASP.NET Core API based on Clean Architecture, CQRS (MediatR), explicit result modeling (MessageResult), structured error contracts, and full observability (CorrelationId + ProblemDetails + request/body/exception logging).

---

# 1️⃣ Architecture Overview

## High-Level Architecture

```text
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

## Layers

### Domain
- `MessageResult`
- `MessageResult<T>`
- `ErrorData`, `ErrorKind`
- Domain Errors catalog (`Errors.*`)

### Application
- `ICommand`, `IQuery`
- `ITransactionalCommand`
- Handlers
- FluentValidation validators
- MediatR pipeline behaviors (logging/validation/caching/uow)

### Infrastructure
- `EfUnitOfWork` (transaction boundary + DB retry-friendly)
- Repositories
- AutoMapper profiles
- DB exception mapping (unique/FK/concurrency/unexpected → `ErrorData`)
- Outbox (reliable downstream dispatch)
- Optional: Query cache backend (in-memory / Redis)
- Optional: Failed HTTP payload store (Redis/SQL/None) with TTL (for support/debug)

### API (Presentation)
- Middlewares (CorrelationId, request logging, body capture, global exception)
- HTTP result mapping (`ApiResponse` / `ProblemDetails`)
- CorrelationId propagation into outbound `HttpClient`
- API versioning helpers

---

# 2️⃣ Request Flow (Real Execution Order)

## HTTP Pipeline Registration Order

> Important: middleware order below is **registration order** (`app.Use...`).  
> Request goes top→down. Response/exception unwinds bottom→up.

1. `CorrelationIdMiddleware`
2. `RequestLoggingMiddleware`
3. `BodyOnErrorLoggingMiddleware`
4. `GlobalExceptionMiddleware`
5. Controllers / minimal endpoints

### Why this order?
- `GlobalExceptionMiddleware` must be **inside** the pipeline so it can convert unhandled exceptions into `ProblemDetails`.
- `BodyOnErrorLoggingMiddleware` must wrap *around* the exception handler to capture the response body produced by `GlobalExceptionMiddleware`.
- `RequestLoggingMiddleware` stays early to log final status/latency for *every* request.

## MediatR Pipeline Order

Recommended registration order (outer → inner):

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

Notes:
- `QueryCachingBehavior` should short-circuit before hitting DB for cache hits.
- `UnitOfWorkBehavior` must run around command handlers to control transactions/SaveChanges.
- Keep logging outermost so it measures whole application pipeline latency and captures result state.

---

# 3️⃣ MessageResult Contract

## Why MessageResult Exists
- No exceptions as business flow
- Explicit Partial Success support
- Predictable mapping to HTTP
- Stable error structure for clients

## States

### Success
```csharp
MessageResult.Ok()
MessageResult.Ok(value)
```

### Partial
```csharp
MessageResult.Partial(warnings)
MessageResult.Partial(value, warnings)
```
Use when:
- DB commit succeeded
- Downstream integration failed
- Non-critical step failed

### Failure
```csharp
MessageResult.Fail(errors)
MessageResult<T>.Fail(errors)
```
Use for:
- Validation errors
- Not found
- Domain violations
- DB constraint errors
- Integration failures (when not partial)

---

# 4️⃣ Error Contract

## ErrorData Structure
- `Code` → stable numeric code
- `Key` → localization key
- `Args` → formatting parameters
- `Description` → fallback text
- `ExtendedErrors` → nested field errors

## Localization Resolution Order
1. RESX by `Key`
2. `Description`
3. `Key`

## HTTP Status Mapping Rules
Priority:
1. Explicit mapping by `Code`
2. Explicit mapping by `Key`
3. Heuristics

Examples:
- `*.not_found` → 404
- `*.validation*` → 400
- unique constraint / duplicate → 409
- FK constraint → 409 / 400 (depending on API contract)
- downstream transport failure → 502
- unexpected → 500

---

# 5️⃣ Unit of Work Behavior

## Core Principles
- Handlers **DO NOT** call `SaveChanges`
- Handlers **DO NOT** open transactions manually
- Retry logic must not be broken by manual transaction management

## EfUnitOfWork Features
- Nested transaction scope
- Rollback-requested flag
- Post-save action queue (run only after successful commit)
- DB exception mapping:
  - Unique constraint
  - FK constraint
  - Concurrency conflict
  - Unexpected

## Golden Rule
> Transaction boundary is controlled by pipeline, not handler.

---

# 6️⃣ Observability

## CorrelationId
Header:
```text
X-Correlation-ID
```

Behavior:
- If provided → propagated (sanitized, length-limited)
- If missing → generated
- Always returned in response header

Injected into:
- Logger scope (`correlationId`, `traceId`, `spanId`, `requestId`)
- `Activity` baggage/tags (OpenTelemetry ready)
- Downstream `HttpClient` (delegating handler)

## Log taxonomy (what logs what)

### HTTP access / traffic summary — `RequestLoggingMiddleware` (EventId=1001)
Logs (structured):
- `Method`, `Path`, optional redacted `Query`
- `StatusCode`
- `ElapsedMs`
- `Endpoint`
- Optional redacted `Headers` (allow-list + deny-list + truncation)

Typical use:
- Traffic monitoring
- Slow requests detection
- Routing to alerting based on status codes

### Application request outcome — `RequestLoggingBehavior`
Logs:
- `RequestName`
- `ElapsedMs` for application pipeline
- Result state: OK / PARTIAL / FAILED (+ error codes/keys)

Typical use:
- Which use-cases fail most often
- Business-level SLOs

### Payload capture — `BodyOnErrorLoggingMiddleware` (EventId=1003)

Modes:
- `Off`: disabled
- `OnServerError`: captures when status >= 500 (unless forced)
- `OnError`: captures when status >= 400 (unless forced)
- `Always`: captures on every request (not recommended for prod)

Policy (per request):
- `Default`: follow `Mode`
- `Force`: capture even for 2xx/3xx/4xx (explicit debugging)
- `Suppress`: never capture

Request body capture rules (to avoid heavy buffering):
- Only for POST/PUT/PATCH
- Only when `Content-Type` is in allow-list
- Only when `Content-Length <= MaxRequestContentLengthToCapture`
  (or `AllowUnknownContentLength=true` when Content-Length is missing)

Safety:
- `MaxBytes` hard cap + truncation for both request/response
- Content-type allow-list (recommended)
- JSON deny-path redaction (recommended)
- Never blocks request: store failures are swallowed

Store (support/debug, TTL):
- `BodyLogging:Store:Mode = None | Redis | Sql`
- When `Mode != None`, the middleware persists `FailedHttpPayload` and sets `HttpContext.Items["__BodyLogKey"] = <key>` for later lookup by support/tools.
- TTL:
  - Redis: native TTL.
  - SQL: retention cleanup is best-effort (e.g., periodic `ExecuteDelete` by `CreatedAtUtc`) or handled by a dedicated job/worker.

Production recommendation:
- Prefer storing bodies in Redis/SQL with TTL
- In normal log stream, log mainly `PayloadKey`, status, endpoint, and whether payload was truncated/size-limited
  (full bodies only when explicitly needed / on forced debugging)
Tip:
- Keep body logging to minimum in prod; use PayloadKey + store lookup workflow for support.
- Full bodies in logs are acceptable only in controlled environments (dev/staging) or temporary incident mode.

### Unhandled exceptions — `GlobalExceptionMiddleware` (EventId=1002)
- Converts exceptions to `ProblemDetails`
- DEV may include extra details
- `OperationCanceledException` (client aborted) → 499

---

# 7️⃣ HTTP Response Contract

## Success
```http
200 OK
```
```json
{
  "value": { },
  "warnings": []
}
```

## Partial Success
```http
200 OK
```
```json
{
  "value": { },
  "warnings": [ ]
}
```

## Failure
```http
4xx / 5xx
```
`ProblemDetails` includes:
- `errors`
- `warnings` (if present)
- `traceId`
- `correlationId`

---

# 8️⃣ Integration Patterns

## Pattern 1 — Sync Integration (Simple)
Handler:
```text
Save DB
Call external API
If API fails → Partial
```

Risk:
- External API fails after DB commit

## Pattern 2 — Outbox (Recommended Production Pattern)
Flow:
```text
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

For create operations client sends:
```text
Idempotency-Key: <GUID>
```

Server:
- Store key + result fingerprint (or full response)
- On repeat → return stored response

Prevents:
- Duplicate orders
- Double dispatch

---

# 🔟 Retry Strategy (HttpClient + Polly)

Recommended policies:
- Retry: transient HTTP failures, timeouts, 5xx, 429
- Circuit Breaker: protect system during downstream outage
- Timeout: hard timeout per call

---

# 1️⃣1️⃣ Caching (Optional)

If enabled:
```text
Caching:UseRedis = true
```

Behavior:
- Query-level caching (queries only)
- TTL per query
- Optional CacheNotFound TTL
- Global or scoped cache backend

---

# 1️⃣2️⃣ Production Readiness Checklist

## Mandatory
- [x] CorrelationId end-to-end
- [x] Structured logging (JSON)
- [x] Localized errors (RESX + fallback)
- [x] Stable error codes
- [x] ProblemDetails compliance
- [x] DB exception mapping
- [x] Transaction boundary centralized (UoW behavior)
- [x] Partial success support
- [x] Outbox pattern for integrations

## Recommended
- [ ] Idempotency store
- [ ] Polly retry/circuit breaker/timeout
- [ ] Rate limiting
- [ ] Health checks
- [ ] OpenTelemetry exporter
- [ ] Central log aggregation (ELK / Seq / Loki)
- [ ] Security hardening (headers, CORS, input limits)
- [ ] Optional: Failed HTTP payload store with TTL (for support/debug)
- [ ] Optional: Payload redaction (deny-paths) + content-type allow-list

---

# 1️⃣3️⃣ Adding New Use Case

### Query
```csharp
public sealed record GetOrderQuery(...) : IQuery<OrderDto>;
```

### Command
```csharp
public sealed record CreateOrder(...) : ICommand<OrderDto>;
```

If needs transaction:
```csharp
public sealed record CreateOrder(...) : ITransactionalCommand<OrderDto>;
```

### Add Validator
```csharp
sealed class CreateOrderValidator : AbstractValidator<CreateOrder> { }
```

### Handler Returns
```csharp
return MessageResult.Ok(...);
return MessageResult.Fail(...);
return MessageResult.Partial(...);
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

## Run
1. Set connection string in `src/MyApp.Api/appsettings.json`
2. `dotnet restore`
3. `dotnet run --project src/MyApp.Api`

> Then adapt namespaces if needed.
