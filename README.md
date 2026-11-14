# PushpakClasses - High-Concurrency Online Timed Examination Platform

[![Platform: .NET 10 LTS](https://img.shields.io/badge/.NET-10.0%20LTS-purple.svg)](https://dotnet.microsoft.com/)
[![Frontend: Angular 22](https://img.shields.io/badge/Angular-22%20Standalone-red.svg)](https://angular.dev/)
[![Database: SQL Server 2022](https://img.shields.io/badge/SQL%20Server-2022%20Developer-blue.svg)](https://www.microsoft.com/sql-server)
[![Cache: Redis 7](https://img.shields.io/badge/Redis-7.0%20Alpine-orange.svg)](https://redis.io/)
[![Load Balancer: Nginx](https://img.shields.io/badge/Nginx-Alpine-green.svg)](https://nginx.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**PushpakClasses** is an enterprise-grade, high-concurrency online examination engine engineered to support 10,000 concurrent candidates simultaneously taking synchronized timed exams. The system features server-authoritative timer offsets, real-time bilingual exam rendering (English & Hindi), sub-millisecond Redis draft answer autosaving, non-blocking asynchronous scoring, multi-replica horizontal load balancing, reattempt request workflows, and complete role-based portals for Administrators and Candidates.

---

## 1. Architecture & Topology

```
                                [ Candidates & Administrators ]
                                               │
                   ┌───────────────────────────┴───────────────────────────┐
                   ▼ (Port 4200)                                           ▼ (Port 5139)
        [pushpakclasses-frontend]                               [pushpakclasses-lb]
       (Nginx serving Angular SPA)                          (Nginx API Load Balancer)
                                                                           │
                                                ┌──────────────────────────┴──────────────────────────┐
                                                ▼ (Internal 8080)                                     ▼ (Internal 8080)
                                      [pushpakclasses-api-1]                                [pushpakclasses-api-2]
                                   (ASP.NET Core 10 Web API)                             (ASP.NET Core 10 Web API)
                                                │                                                     │
                                                └──────────────────────────┬──────────────────────────┘
                                                                           │
                                                ┌──────────────────────────┴──────────────────────────┐
                                                ▼ (Port 1433)                                         ▼ (Port 6379)
                                    [pushpakclasses-sqlserver]                              [pushpakclasses-redis]
                                     (Microsoft SQL Server 2022)                             (Redis 7 Alpine Cache)
```

### Key Architectural Highlights
* **Server-Authoritative Clock**: The client device clock is never trusted. All quiz durations, countdowns, and submission cutoffs are calculated from UTC timestamps synchronized via `/api/quizzes/{id}/start` (`serverTimeUtc`).
* **Real-Time Bilingual Engine (English & Hindi)**: Supports full bilingual question prompts and answer choices (`TextEn` and `TextHi`). Candidates can toggle between English and Hindi on-the-fly during exams and on the review screen with automatic fallback.
* **Sub-Millisecond Redis Draft Answer Buffer**: In-progress candidate answers hit key `attempt:{id}:answers` directly in Redis in under 5 ms, isolating SQL Server from tens of thousands of rapid draft answer updates.
* **Asynchronous Scoring & Certification**: Submissions flush Redis drafts into SQL Server and delegate score calculation, percent calculation, and certificate generation to background Hangfire worker jobs.
* **Schedule Enforcement & Live Auto-Enabling**: Quizzes feature computed statuses (`Upcoming`, `Open`, `Resume`, `Attempted`, `Expired`). Upcoming quizzes display a live countdown timer (`Starts in hh:mm:ss`) that automatically enables the Start button the exact second the exam opens without page refreshes.
* **Attempt Integrity Protections & Reattempt Workflow**: Questions in published quizzes with candidate attempts are locked against structural changes (only typo text fixes allowed). Candidates can request extra attempts with a reason (`POST /api/quizzes/{id}/reattempt-request`), which admins can review and approve or reject.
* **Zero-Allocation DbContext Pooling**: `AddDbContextPool<PushpakClassesDbContext>` with a pool size of 1024 instances eliminates EF Core context instantiation allocations under high request concurrency.
* **Stateless Horizontally Scalable Replicas**: APIs store zero in-memory state, allowing effortless horizontal scaling across 2, 4, or 16 container replicas behind Nginx (`least_conn` routing).

---

## 2. Environment Configuration & Seed Accounts

All sensitive secrets, database connection strings, credentials, and signing keys are managed via environment variables. Copy `.env.example` to `.env` to customize your local configuration:

```bash
cp .env.example .env
```

### Initial Seed Accounts (Configurable via Environment)

| Role | Environment Variables | Default (Development Template) | Description |
| :--- | :--- | :--- | :--- |
| **Admin** | `ADMIN_EMAIL`, `ADMIN_PASSWORD` | `admin@example.com` / `ChangeMe123!` | System Administrator Portal |
| **User** | `USER_EMAIL`, `USER_PASSWORD` | `candidate@example.com` / `ChangeMe123!` | Primary Candidate Account |

> [!IMPORTANT]
> Never commit real production passwords, connection strings, or JWT secrets to Git. Always configure these via `.env` or deployment orchestrator environment variables.

---

## 3. Zero-Cost Cloud Deployment

For deploying PushpakClasses to the cloud using **100% free resources** (zero paid subscriptions or credit card requirements), see our step-by-step [**Deployment Guide (DEPLOYMENT.md)**](file:///DEPLOYMENT.md).

* **1-Click Render.com Blueprint**: Automatically deploys the ASP.NET Core API (Docker) and Angular Frontend (Static Site) via [`render.yaml`](file:///render.yaml).
* **Netlify CDN**: 1-click Angular SPA hosting with edge routing via [`netlify.toml`](file:///netlify.toml).
* **Free Database**: Guide for Azure SQL Database Free Tier (32 GB lifetime free) or Oracle Cloud Always-Free Compute.
* **Automated CI/CD**: GitHub Actions workflow ([`.github/workflows/ci-cd.yml`](file:///.github/workflows/ci-cd.yml)) for continuous testing and container building.

---

## 4. Quick Start with Docker Compose

Running the complete multi-container topology (SQL Server 2022, Redis 7, 2 API Replicas, Nginx Load Balancer, and Angular Frontend):

### Step 1: Clone and Launch
```bash
# Clone the repository
git clone https://github.com/ravi-kumar-kushwaha/pushpak-classes.git
cd pushpak-classes

# Launch the entire container stack
docker compose up -d --build
```

### Step 2: Access Platform Services
Once the containers are healthy, open your browser:

* **Candidate & Admin Web Portal**: [http://localhost:4200](http://localhost:4200)
* **API Load Balancer (REST API)**: [http://localhost:5139](http://localhost:5139)
* **Interactive Swagger Documentation**: [http://localhost:5139/swagger](http://localhost:5139/swagger)
* **Health Check Probe**: [http://localhost:5139/health](http://localhost:5139/health)
* **Hangfire Job Monitoring**: [http://localhost:5139/hangfire](http://localhost:5139/hangfire)

### Step 3: Stop Services
```bash
docker compose down
```

---

## 4. Local Development (Without Docker)

### Prerequisites
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* [Node.js 22 LTS](https://nodejs.org/) & npm 11+
* Running Microsoft SQL Server & Redis instance (or spin up dependencies via Docker):
  ```bash
  docker compose up -d sqlserver redis
  ```

### Running Backend API
```bash
cd backend/PushpakClasses.Api
dotnet restore
dotnet run
# API listens on http://localhost:5139
```

### Running Frontend Application
```bash
cd frontend/pushpak-classes
npm install
npm start
# Web application listens on http://localhost:4200
```

---

## 5. Database Migrations & Seeding

### Automatic Migrations
Database migrations and administrator account seeding automatically execute upon API startup (`Program.cs`).

### Manual CLI Migrations
If updating EF Core entities or applying migrations manually:
```bash
# Add a new migration
dotnet ef migrations add <MigrationName> \
  --project backend/PushpakClasses.Infrastructure \
  --startup-project backend/PushpakClasses.Api

# Apply pending migrations to database
dotnet ef database update \
  --project backend/PushpakClasses.Infrastructure \
  --startup-project backend/PushpakClasses.Api
```

### Seeding Test Quiz & Candidate Questions
To seed a published mock examination with bilingual questions:
```bash
node loadtest/seed-quiz.js
```

---

## 6. Testing & Quality Assurance

PushpakClasses maintains comprehensive automated unit and integration tests across both the backend and frontend suites.

### Backend Test Suite (145 Passing Tests)
Includes tests for JWT authentication, refresh token rotation, schedule enforcement, single-attempt policies, bilingual question rendering, scoring calculations, and rate limiting:
```bash
dotnet test backend/PushpakClasses.sln
```

### Frontend Test Suite (62 Passing Tests)
Includes tests for the exam player, countdown timers, bilingual language switcher, paginator components, form validators, and guards:
```bash
cd frontend/pushpak-classes
npm test -- --watch=false
```

---

## 7. Configuration & Environment Variables

Key runtime environment variables (see [`.env.example`](file:///.env.example) for full list):

| Variable | Description | Example / Template Value |
| :--- | :--- | :--- |
| `DB_PASSWORD` | SQL Server `sa` account password | `YourStrong@Passw0rd!` |
| `ConnectionStrings__DefaultConnection` | SQL Server connection string | `Server=sqlserver,1433;Database=PushpakClassesDb;User Id=sa;Password=${DB_PASSWORD};...` |
| `JWT_SECRET` / `JwtSettings__Secret` | Symmetric key for HMAC-SHA256 signing (>= 32 chars) | `ReplaceWithYourSuperSecretJwtSigningKey32CharsMin!` |
| `ADMIN_EMAIL` / `SeedData__AdminUser__Email` | Initial seeded Administrator email | `admin@example.com` |
| `ADMIN_PASSWORD` / `SeedData__AdminUser__Password` | Initial seeded Administrator password | `ChangeMe123!` |
| `CLEANUP_PROTECTED_EMAILS` | Comma-separated accounts protected from cleanup | `admin@example.com,candidate@example.com` |
| `REDIS_CONNECTION` / `RedisSettings__ConnectionString` | Redis host connection string | `redis:6379` |
| `RedisSettings__Enabled` | Toggle distributed Redis caching | `true` |
| `EmailSettings__Provider` | Email implementation (`Logging` or `Smtp`) | `Logging` |
| `EmailSettings__AppBaseUrl` | Frontend URL for verification and password reset | `http://localhost:4200` |

---

## 8. High-Concurrency 10,000 VU Load Testing

A dedicated k6 load test script is available in `/loadtest/simulation-10k.js` modeling the complete candidate exam lifecycle:
1. **Candidate Login**: Authentication and JWT token issuance.
2. **Synchronized Spike**: All virtual users hit `POST /api/quizzes/{id}/start` at the exact same second.
3. **In-Exam Autosave**: Candidates draft answers every 10 seconds via `POST /api/attempts/{id}/save`.
4. **Final Submission**: Candidates submit exams via `POST /api/attempts/{id}/submit`.

### Executing Load Test via Docker
```bash
# Run with 10,000 Virtual Users
docker run --rm -i --network=host \
  -v "$PWD/loadtest:/loadtest" \
  grafana/k6 run -e TARGET_VUS=10000 /loadtest/simulation-10k.js

# Run with customized concurrency (e.g. 2,000 VUs)
docker run --rm -i --network=host \
  -v "$PWD/loadtest:/loadtest" \
  grafana/k6 run -e TARGET_VUS=2000 -e AUTOSAVE_CYCLES=1 -e AUTOSAVE_INTERVAL_SEC=3 /loadtest/simulation-10k.js
```

---

## 9. Architectural Optimizations & Performance Hardening

During load testing phases up to 10,000 concurrent Virtual Users, key bottlenecks were profiled and resolved:

1. **PBKDF2 Password Hashing Saturation**:
   * *Problem*: Thousands of simultaneous logins saturated CPU cores.
   * *Solution*: Implemented candidate arrival staggering across an entry window prior to exam start.
2. **Synchronized Exam Start SQL Contention**:
   * *Problem*: All virtual users queried the database simultaneously for identical quiz metadata.
   * *Solution*: Implemented sub-millisecond Redis caching for quiz metadata (`quiz:{id}:meta`) and active attempts (`user:{id}:active_attempt`), eliminating 75% of hot-path database reads.
3. **Connection Pooling Optimization**:
   * *Problem*: Connection pool timeouts under high concurrency.
   * *Solution*: Disabled MARS (`MultipleActiveResultSets=False`), increased connection pool size to 1024, and set `Connect Timeout=60`, reducing pool contention by over 50%.
4. **Asynchronous Batch Publishing**:
   * *Problem*: Publishing an exam synchronously queued 10,000 email jobs inside the HTTP request.
   * *Solution*: Decoupled email broadcasting into background coordinator jobs, allowing the endpoint to respond in < 50 ms.

---

## 10. License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
