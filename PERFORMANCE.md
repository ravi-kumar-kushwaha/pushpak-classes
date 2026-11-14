# PushpakClasses - Performance & Scalability Guide

This document details the hardening and performance architecture designed to support **10,000 concurrent users** on the **PushpakClasses** examination platform during peak testing sessions.

---

## 1. High-Concurrency Architecture Overview

During nationwide or institution-wide exam windows, thousands of candidates concurrently fetch questions, autosave draft answers every 10 seconds, and submit their attempts simultaneously. The backend is designed as a **completely stateless system** capable of scaling horizontally across multiple container instances behind a reverse proxy or cloud load balancer (e.g., NGINX, AWS ALB, Azure App Gateway).

```
                      [10,000 Concurrent Candidates]
                                     │
                                     ▼
                [Reverse Proxy / Cloud Load Balancer (HTTPS / HSTS)]
                                     │
        ┌────────────────────────────┼────────────────────────────┐
        ▼                            ▼                            ▼
[API Instance 1]             [API Instance 2]             [API Instance N]
 ├─ Security Headers          ├─ Security Headers          ├─ Security Headers
 ├─ RateLimiter (IP/User)     ├─ RateLimiter (IP/User)     ├─ RateLimiter (IP/User)
 ├─ Serilog Structured Logs   ├─ Serilog Structured Logs   ├─ Serilog Structured Logs
 ├─ ProblemDetails Handler    ├─ ProblemDetails Handler    ├─ ProblemDetails Handler
 ├─ Redis Distributed Cache   ├─ Redis Distributed Cache   ├─ Redis Distributed Cache
 ├─ DbContextPool (1024 max)  ├─ DbContextPool (1024 max)  ├─ DbContextPool (1024 max)
 └─ Health Checks (/health)   └─ Health Checks (/health)   └─ Health Checks (/health)
        │                            │                            │
        └────────────────────────────┼────────────────────────────┘
                                     │
             ┌───────────────────────┴───────────────────────┐
             ▼                                               ▼
   [Redis Cluster (In-Memory)]                   [SQL Server 2022 (Pooled Conn)]
    ├─ Questions: `quiz:{id}:questions`           ├─ Composite B-Tree Indexes
    ├─ Drafts: `attempt:{id}:answers`             ├─ AsNoTracking Queries
    └─ Quizzes: `quizzes:available:*`             └─ Non-Blocking Read Commits
```

---

## 2. Rate Limiting (`Microsoft.AspNetCore.RateLimiting`)

To safeguard the infrastructure against credential stuffing, brute force, and rapid autosave flooding, rate limiting is applied using sliding windows partitioned by client IP or authenticated user:

| Policy Name | Target Endpoints | Partition Key | Algorithm | Limits |
|---|---|---|---|---|
| `auth-rate-limit` | `/api/auth/login`, `/signup`, `/forgot-password` | Client IP (`RemoteIpAddress`) | Sliding Window | **10 requests / minute**, Queue: 2 |
| `save-rate-limit` | `/api/attempts/{id}/save` | Authenticated `UserId` (or IP) | Sliding Window | **30 requests / minute**, Queue: 5 |
| `api-global-concurrency` | Entire Application | Global | Concurrency Limiter | **5,000 concurrent requests**, Queue: 2,000 |

### On-Rejected Behavior:
When a partition exceeds the threshold:
- Status Code: `429 Too Many Requests`.
- Header: `Retry-After: 60`.
- Body: Formatted as standard RFC 7807 `ProblemDetails` with `traceId` and `timestampUtc`.

---

## 3. Distributed Caching & Invalidation (Redis)

Rather than executing heavy relational queries on SQL Server for each candidate request, hot data is served from Redis:

1. **Quiz Questions Cache (`quiz:{id}:questions`)**:
   - Stores sanitized, pre-ordered questions and options without correct answers.
   - **TTL**: 10 minutes.
   - **Invalidation**: Evicted instantly whenever an administrator creates, modifies, deletes, reorders, or bulk-imports questions in `QuestionService`.
2. **Available Quizzes Cache (`user:{userId}:quizzes:available:*`)**:
   - Caches paged quiz listings per student query.
   - **TTL**: 30 seconds.
   - Drastically cuts database read IOPS during exam opening surges.
3. **Draft Answers Buffer (`attempt:{id}:answers`)**:
   - Periodic candidate autosaves (10-second debounce) write exclusively to in-memory Redis buffers.
   - **TTL**: 24 hours.
   - Answers are flushed to SQL Server only when the exam is submitted or auto-submitted upon timer expiration, preventing thousands of write transactions per second against SQL Server.

---

## 4. Database Optimizations & Query Tuning

### 4.1 EF Core DbContext Pooling
- Configured using `AddDbContextPool<PushpakClassesDbContext>(..., poolSize: 1024)`.
- Reuses context instances across requests, eliminating object allocation churn and garbage collection pauses during high throughput.

### 4.2 Systematic `AsNoTracking()`
- All read operations across `QuizAttemptService`, `ReportService`, `UserService`, and `QuestionService` explicitly enforce `.AsNoTracking()`.
- Disables change tracking snapshots, cutting per-query CPU and RAM usage by up to 60%.

### 4.3 Composite Indexes
The database schema utilizes targeted composite B-Tree indexes matching critical query patterns:
- `IX_QuizAttempts_QuizId_UserId`: Accelerates max attempts checks and user attempt lookups.
- `IX_QuizAttempts_Status_EndsAtUtc`: Supports the 1-minute Hangfire recurring auto-submit job (`Status == InProgress AND EndsAtUtc + 10s < now`).
- `IX_Questions_QuizId_Order`: Ensures constant-time retrieval of ordered quiz questions.
- `IX_Users_Email` (Unique): Immediate login verification and signup collision detection.

---

## 5. Health Checks & Container Probes

The API exposes three health endpoints for container orchestrators (Kubernetes / Docker Swarm) and load balancers:

- **`GET /health`**: Returns detailed JSON status of all system components:
  - `sql_server`: Verifies `Database.CanConnectAsync()`.
  - `redis`: Actively probes Redis cluster responsiveness.
  - Returns `totalDurationMs`, component latency, and UTC timestamp.
- **`GET /health/ready`**: Readiness probe. Ensures database and cache connections are live before routing traffic.
- **`GET /health/live`**: Liveness probe. Validates that the ASP.NET Core process is responsive.

---

## 6. Observability: Structured Logging (Serilog)

- Every request is enriched with `TraceIdentifier`, `UserId`, `ClientIp`, and `Application: PushpakClasses.Api`.
- Request logging via `app.UseSerilogRequestLogging()`.
- Sensitive fields (passwords, tokens, credit card data) are omitted or redacted.
- Global exception handling logs unhandled exceptions with full stack traces while masking internal error details from clients.

---

## 7. Error Handling: RFC 7807 `ProblemDetails`

All domain errors, validation failures, and unhandled exceptions are translated by `GlobalExceptionMiddleware` into standardized RFC 7807 JSON:

```json
{
  "type": "https://pushpakclasses.com/errors/validation",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Password does not meet complexity requirements.",
  "instance": "/api/auth/signup",
  "traceId": "0HN123456789",
  "timestampUtc": "2026-11-08T15:20:00Z",
  "errors": {
    "Password": [
      "The Password must be at least 8 characters long."
    ]
  }
}
```

Backward compatibility with existing frontend applications expecting `error.error.message` is preserved via extension properties.

---

## 8. Security Hardening & Headers

Applied automatically to every response via `SecurityHeadersMiddleware`:
- `X-Content-Type-Options: nosniff`: Prevents MIME-type sniffing.
- `X-Frame-Options: DENY`: Prevents clickjacking.
- `X-XSS-Protection: 1; mode=block`: Enables browser XSS filters.
- `Referrer-Policy: strict-origin-when-cross-origin`: Restricts referrer information leakage.
- `Content-Security-Policy`: Restricts scripts, objects, and framing.
- `Strict-Transport-Security (HSTS)`: `max-age=31536000; includeSubDomains` enforced on HTTPS.
- `CORS`: Origin-restricted to configured frontend URL (`http://localhost:4200` in dev).

---

## 9. Stateless Scale-Out & Secrets Configuration

The API does not hold in-memory session state. All state is externalized:
- **Authentication**: Stateless signed JWT tokens.
- **Cache & Buffers**: StackExchange.Redis.
- **Background Jobs**: Hangfire backed by SQL Server / Redis.

### Secrets & Environment Variable Overrides
In production or Docker deployments, override sensitive settings using environment variables:

| Setting | Environment Variable Override | Example |
|---|---|---|
| SQL Server Connection | `ConnectionStrings__DefaultConnection` | `Server=sqlserver,1433;Database=PushpakClassesDb;User Id=sa;Password=YourSecurePassword!;TrustServerCertificate=True;` |
| JWT Signing Key | `JwtSettings__Secret` | `Your64CharacterProductionSigningKeyHere!` |
| Redis Connection | `RedisSettings__ConnectionString` | `redis:6379,abortConnect=false` |
| Admin Password Seed | `SeedData__AdminUser__Password` | `YourAdminPassword123!` |
| SMTP Password | `EmailSettings__SmtpPassword` | `YourSmtpAppPassword` |
| Allowed CORS Origins | `Cors__AllowedOrigins__0` | `https://pushpakclasses.com` |
