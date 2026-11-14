# PushpakClasses Zero-Cost Cloud Deployment Guide

This guide details how to deploy the entire **PushpakClasses** platform to production using **100% free cloud resources** with zero paid services or subscriptions required.

---

## Architecture Overview on Free Cloud Tiers

```
  [Candidate / Admin Browser]
              │
              ▼
  ┌──────────────────────────────────────────────┐
  │  Frontend: Angular 22 Single Page App        │
  │  Hosted on Netlify / Render Static (FREE)    │
  │  - Global Anycast CDN, Automated HTTPS       │
  └──────────────────────┬───────────────────────┘
                         │ API Requests (HTTPS)
                         ▼
  ┌──────────────────────────────────────────────┐
  │  Backend API: ASP.NET Core 10 Web API        │
  │  Hosted on Render Free Web Service (FREE)    │
  │  - Built via Docker from GitHub              │
  │  - In-Memory Redis Fallback & Hangfire Queue │
  └──────────────────────┬───────────────────────┘
                         │ EF Core SQL Server
                         ▼
  ┌──────────────────────────────────────────────┐
  │  Database: Microsoft SQL Server              │
  │  - Azure SQL Database Free Tier (32 GB)      │
  │    OR Oracle Cloud Always-Free VM (24GB RAM) │
  └──────────────────────────────────────────────┘
```

---

## Method 1: Render.com 1-Click Blueprint (Recommended)

Render provides free static web hosting and free Docker web services directly connected to your GitHub repository.

### Step 1: Sign Up on Render
1. Create a free account at [render.com](https://render.com) (no credit card required).
2. Connect your GitHub account.

### Step 2: Deploy via Blueprint (`render.yaml`)
1. From the Render Dashboard, click **New +** and select **Blueprint**.
2. Select your repository: `ravi-kumar-kushwaha/pushpak-classes`.
3. Render will automatically detect [`render.yaml`](file:///render.yaml) and configure:
   - `pushpakclasses-web` (Free Angular Static Site)
   - `pushpakclasses-api` (Free Docker Web API)
4. Click **Apply**.

### Step 3: Configure Environment Variables
In the Render dashboard under `pushpakclasses-api` > **Environment**, configure:
- `ConnectionStrings__DefaultConnection`: Your free database connection string (see Database options below).
- `SeedData__AdminUser__Password`: Choose your initial administrator password (e.g. `StrongAdmin@2026!`).

---

## Method 2: Free Database Options for SQL Server

Because PushpakClasses is engineered with Entity Framework Core for SQL Server, choose one of these 100% free database solutions:

### Option A: Azure SQL Database Free Offer (Lifetime Free)
Microsoft provides a permanent free tier for Azure SQL Database:
- **Storage**: 32 GB included free.
- **Compute**: 100,000 vCore seconds/month serverless auto-pausing.
- **How to create**:
  1. Go to [portal.azure.com](https://portal.azure.com) > **Create a resource** > **Azure SQL Database**.
  2. Under **Compute + storage**, select **General Purpose - Serverless**.
  3. Check the box **"Want to try Azure SQL Database for free? 100,000 vCore seconds free each month"**.
  4. Enable firewall access: **Allow Azure services and resources to access this server**.
  5. Copy the ADO.NET connection string into your Render API environment:
     ```
     Server=tcp:your-server.database.windows.net,1433;Initial Catalog=PushpakClassesDb;Persist Security Info=False;User ID=your_admin;Password=YourPassword!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
     ```

### Option B: Oracle Cloud Always-Free Compute (Complete Stack Hosting)
Oracle Cloud Infrastructure (OCI) offers an **Always Free Tier** with:
- **4 Ampere A1 ARM OCPUs** and **24 GB of RAM** (free forever).
- **200 GB block storage**.
- You can run the entire PushpakClasses Docker Compose stack (`sqlserver`, `redis`, `api`, `frontend`, `lb`) in a single command on this free VM:
  ```bash
  git clone https://github.com/ravi-kumar-kushwaha/pushpak-classes.git
  cd pushpak-classes
  cp .env.example .env
  # Set your secure DB_PASSWORD in .env
  docker compose up -d
  ```

---

## Method 3: Netlify for Frontend (100% Free CDN)

If you prefer hosting the Angular frontend on Netlify:
1. Sign up at [netlify.com](https://netlify.com) using your GitHub account.
2. Click **Add new site** > **Import an existing project** > **GitHub**.
3. Choose `ravi-kumar-kushwaha/pushpak-classes`.
4. Netlify will detect [`netlify.toml`](file:///netlify.toml) automatically:
   - **Base directory**: `frontend/pushpak-classes`
   - **Build command**: `npm install && npm run build -- --configuration=production`
   - **Publish directory**: `frontend/pushpak-classes/dist/pushpak-classes/browser`
5. Click **Deploy Site**.
6. In `index.html` or site settings, point `window.__env.apiUrl` to your live API endpoint.

---

## Method 4: Free Redis Cache (Optional)

PushpakClasses includes a high-performance **in-memory fallback** that activates automatically when `RedisSettings__Enabled=false`, requiring zero external cache.

If you desire a distributed cloud Redis cache for free:
1. Sign up at [upstash.com](https://upstash.com) (Serverless Redis, 100% free tier, 10,000 requests/day, no credit card required).
2. Create a new Redis database.
3. Copy the **Primary Connection String** (e.g. `rediss://default:token@region.upstash.io:port`).
4. Set in your API environment variables:
   - `RedisSettings__ConnectionString`: `rediss://default:token@region.upstash.io:port,ssl=true,abortConnect=false`
   - `RedisSettings__Enabled`: `true`

---

## Verification & Health Check

Once your services are deployed, verify health:

```bash
# 1. Check API readiness and database migration status
curl -i https://<your-api-domain>/health/ready

# 2. Check Swagger OpenAPI Documentation
https://<your-api-domain>/swagger

# 3. Open Candidate & Admin Frontend
https://<your-frontend-domain>/
```

### Initial Administrator Access:
- **Email**: Configured via `SeedData__AdminUser__Email` (`admin@example.com` by default)
- **Password**: Configured via `SeedData__AdminUser__Password`
