# AGENT_RULES.md - PushpakClasses Engineering & Operating Rules

> **CRITICAL DIRECTIVE**: Every agent, developer, and task working in this repository MUST strictly follow the specifications and rules outlined in this document. No deviations are permitted without explicit user approval.

---

## 1. Project & Brand Identity

- **Brand & Project Name**: `PushpakClasses`
- **Application Type**: High-scale, real-time timed quiz platform
- **Target Concurrency**: **10,000 concurrent users**
- **Strict Brand Naming**:
  - The name **`PushpakClasses`** must be used across all solution files, project names (`PushpakClasses.Domain`, `PushpakClasses.Application`, `PushpakClasses.Infrastructure`, `PushpakClasses.Api`, `PushpakClasses.UnitTests`), page titles, logo text, email templates, Docker image names (`pushpakclasses-api`, `pushpakclasses-frontend`), and documentation / README.
  - Angular application folder / workspace name: `pushpak-classes`.

---

## 2. Technology Stack & Architectural Standards

### 2.1 Backend (.NET)
- **Framework**: ASP.NET Core Web API (.NET latest LTS)
- **Architecture**: Clean Architecture pattern with strict layer decoupling:
  - **`PushpakClasses.Domain`**: Core domain entities, value objects, domain events, domain exceptions, enums. Zero external dependencies.
  - **`PushpakClasses.Application`**: Use cases, CQRS (MediatR) or service layer, DTOs, interface abstractions, FluentValidation validators, AutoMapper profiles, business exceptions.
  - **`PushpakClasses.Infrastructure`**: EF Core DbContext, SQL Server configurations, external services, email delivery, caching (Redis/distributed cache), repository/persistence implementations.
  - **`PushpakClasses.Api`**: REST API controllers / Minimal APIs, middleware (global exception handling, request logging), Serilog configuration, dependency injection setup, OpenAPI/Swagger, JWT authentication & authorization.
- **Data Persistence**: EF Core + Microsoft SQL Server
- **Validation**: FluentValidation (automatic pipeline validation)
- **Object Mapping**: AutoMapper (strictly mapped profiles)
- **Logging**: Serilog (structured JSON logging with correlation IDs, request timing, log enrichment)
- **Concurrency & High-Scale (10,000 Concurrent Users)**:
  - Pure asynchronous execution (`async`/`await`) with non-blocking I/O across all layers.
  - Connection pooling, read-only queries with `AsNoTracking()`, query optimization, and proper database indexing.
  - Caching strategy (e.g., Redis for quiz metadata and active session state) to protect SQL Server under peak concurrency.

### 2.2 Frontend (Angular)
- **Framework**: Angular (latest stable)
- **Architecture**: Standalone components exclusively (avoid legacy NgModules).
- **State Management & Reactivity**: Angular Signals (`signal`, `computed`, `effect`) for reactive state management.
- **Routing**: Lazy-loaded routes (`loadComponent`, `loadChildren`) for optimal initial bundle load time.
- **Forms**: Reactive Forms (`FormBuilder`, typed `FormGroup`, custom validators).
- **Styling & UI**: Bootstrap 5 + responsive custom SCSS.

---

## 3. Mandatory Engineering Rules

### Rule 1: Server-Authoritative Quiz Timer
- The quiz timer is strictly **server-authoritative**.
- Stored and tracked via `StartedAtUtc` and `EndsAtUtc`.
- **NEVER trust client-reported time or remaining duration.**
- When a user starts a quiz, the server computes `EndsAtUtc = StartedAtUtc + Duration`.
- On answer or quiz submission, the backend validates whether `DateTime.UtcNow <= EndsAtUtc` (plus a small server-configured network grace tolerance). Submissions received after the server deadline are rejected or automatically finalized by the server.
- The client timer is strictly for visual display and countdown UX.

### Rule 2: Strict UTC Everywhere
- All timestamps stored in the database, processed in business logic, and transferred in API contracts MUST be in **UTC** (`DateTime` with UTC kind or `DateTimeOffset`).
- Client applications receive UTC timestamps and convert to local time only at the presentation layer for display.

### Rule 3: Zero Entity Leaks (Always DTOs)
- **NEVER** return domain entities directly from API controllers or endpoints.
- **ALWAYS** return strongly typed DTOs (Data Transfer Objects) mapped via AutoMapper or explicit projections.
- Incoming payloads MUST be received as DTOs / Commands / Requests and validated using FluentValidation before processing.

### Rule 4: Universal Pagination, Search, and Sorting on List APIs
- Every list or collection API endpoint MUST support:
  - **Pagination**: `pageNumber` (1-based), `pageSize` (with enforced upper bounds, e.g., max 100).
  - **Search**: `searchQuery` / `searchTerm` filtering across relevant text fields.
  - **Sorting**: `sortBy` (field name) and `sortDirection` (`asc` / `desc`).
- Return a standardized paginated response wrapper (e.g., `PagedResult<T>` with `Items`, `TotalCount`, `PageNumber`, `PageSize`, `TotalPages`, `HasPreviousPage`, `HasNextPage`).

### Rule 5: Role-Based Access Control (RBAC)
- System roles: **`Admin`** and **`User`**.
- Roles are strictly enforced on backend endpoints using `[Authorize(Roles = "Admin")]`, `[Authorize(Roles = "User")]`, or custom authorization policies.
- Frontend route guards and UI visibility toggles exist solely for user experience; the backend is the authoritative security boundary.

### Rule 6: Soft Deletes
- Soft delete (`IsDeleted`, `DeletedAtUtc`, `DeletedBy`) MUST be implemented for:
  - `User`
  - `Quiz`
  - `Question`
- EF Core global query filters (`builder.HasQueryFilter(e => !e.IsDeleted)`) must be configured so soft-deleted entities are automatically excluded from normal queries.
- Hard delete is strictly prohibited for these entities unless explicitly requested by an authorized administrative audit purge.

### Rule 7: Mandatory Unit Testing
- Unit tests MUST be written for every application service, command/query handler, validator, and domain rule.
- Tests must be placed in `PushpakClasses.UnitTests`.
- Utilize modern testing libraries (xUnit, Moq/NSubstitute, FluentAssertions).
- Test suites must cover business edge cases, server-authoritative timer validation, boundary pagination cases, and role access rules.

### Rule 8: Pre-Coding Approval Workflow
- **Before coding each phase or feature, present a clear, step-by-step implementation plan and WAIT for explicit user approval before proceeding.**
- Do not write code or generate project assets until the user has approved the plan for that phase.

---

## 4. Repository Layout & Naming Conventions

```
Pushpak_App/
├── AGENT_RULES.md
├── backend/
│   ├── PushpakClasses.sln
│   ├── PushpakClasses.Domain/
│   ├── PushpakClasses.Application/
│   ├── PushpakClasses.Infrastructure/
│   ├── PushpakClasses.Api/
│   └── PushpakClasses.UnitTests/
├── frontend/
│   └── pushpak-classes/
└── docker/
    ├── docker-compose.yml
    ├── Dockerfile.api
    └── Dockerfile.frontend
```

---

## 5. Execution Protocol Summary

| Phase | Action Required |
|---|---|
| **Any New Phase / Task** | Formulate an implementation plan covering backend, frontend, tests, and database changes. |
| **User Sign-Off** | Wait for user approval before modifying or creating code. |
| **Development** | Strictly adhere to Clean Architecture, Server-Authoritative Timers, UTC timestamps, DTO-only contracts, Soft Deletes, RBAC, and Pagination/Sorting. |
| **Verification** | Write and execute unit tests for all services. |
