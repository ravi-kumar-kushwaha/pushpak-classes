# Render Deployment & SQL Server Persistence Checklist

This checklist guarantees zero data loss on Render by ensuring PushpakClasses connects directly to persistent cloud Microsoft SQL Server storage (e.g., Azure SQL Database) and fails fast if any ephemeral fallback is attempted.

---

## 1. Exact Environment Variables for Render Web Service (`pushpakclasses-api`)

ASP.NET Core configuration binds nested settings using **double underscores** (`__`). Configure these environment variables in your Render Dashboard under **Environment** (or in your Render Blueprint):

| Variable Name | Required | Example / Recommended Value | Notes |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | **YES (CRITICAL)** | `Server=tcp:<server>.database.windows.net,1433;Initial Catalog=PushpakClassesDb;Persist Security Info=False;User ID=<user>;Password=<pass>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;` | **Fail-Fast**: If missing, or if SQLite/in-memory is detected, the API will immediately refuse to start. |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` | Sets production mode. |
| `ASPNETCORE_HTTP_PORTS` | Yes | `8080` | Render expects the container to listen on port 8080. |
| `JwtSettings__Secret` | Yes | `YOUR_SECURE_JWT_SECRET_KEY_AT_LEAST_32_BYTES_LONG_123456` | At least 32 characters for HMAC-SHA256 signing. |
| `SeedData__AdminUser__Email` | Yes | `admin@pushpakclasses.com` | Default admin email to seed if missing. |
| `SeedData__AdminUser__Password` | Yes | `YourStrongAdminPassword123!` | Password for default admin account. |
| `HangfireSettings__UseInMemory` | No | `false` | When `false`, Hangfire stores background jobs in SQL Server tables so jobs survive container restarts. |
| `RedisSettings__Enabled` | No | `false` | Set `false` if not running Redis. Only temporary drafts/cache will use memory; core data stays in SQL Server. |
| `EnableHttpsRedirection` | No | `false` | Render terminates SSL at its edge proxy. |
| `EnableSwagger` | No | `true` (or `false`) | Enable or disable Swagger UI in production. |

---

## 2. Azure SQL Database Configuration & Firewall Setup

If using Azure SQL Database, Render's outgoing servers will be blocked by default unless your Azure SQL firewall allows incoming connections:

1. **Open Azure Portal**: Navigate to your **SQL server** (not just the database).
2. **Go to Networking**: In the left sidebar under **Security**, click **Networking**.
3. **Allow Azure Services / Public Cloud IPs**:
   - Check the box **"Allow Azure services and resources to access this server"**.
   - Under **Firewall rules**, add a rule:
     - **Rule Name**: `AllowRenderCloud`
     - **Start IP**: `0.0.0.0`
     - **End IP**: `255.255.255.255`
     *(This allows cloud providers such as Render with dynamic outbound IPs to connect securely over TLS).*
4. **Ensure Connection String Flags**:
   - `Encrypt=True;`
   - `TrustServerCertificate=False;`
   - `Connection Timeout=60;`
5. **Verify Database Exists**: Ensure the database name specified in `Initial Catalog=` (e.g. `PushpakClassesDb`) is created on Azure SQL before deploying.

---

## 3. How to Verify Connection on Render Startup

Every time the API boots on Render, it automatically runs database migrations and performs a **Persistence Audit**.

1. In Render Dashboard, click **pushpakclasses-api** -> **Logs**.
2. Look for the `[PERSISTENCE AUDIT]` log block:
   ```text
   [PERSISTENCE AUDIT] Connected to SQL Server.
     - Provider: Microsoft.EntityFrameworkCore.SqlServer
     - Server Host: your-server.database.windows.net
     - Database: PushpakClassesDb
     - Existing Record Counts:
         * Users: 1
         * Quizzes: 5
         * Categories: 3
         * Attempts: 12
   ```
3. If the connection fails or if an invalid connection string was supplied, the service will **fail fast** and log:
   ```text
   CRITICAL DATABASE CONFIGURATION ERROR: Non-persistent/SQLite database detected...
   ```
   or
   ```text
   CRITICAL DATABASE CONFIGURATION ERROR: 'ConnectionStrings__DefaultConnection' is missing...
   ```
   This prevents the app from silently running on a temporary local database.

---

## 4. Manual Verification Endpoint: `GET /health/db`

An admin-only verification endpoint is available at `/health/db`:

- **URL**: `https://<your-render-api>.onrender.com/health/db`
- **Method**: `GET`
- **Headers**:
  ```http
  Authorization: Bearer <Admin_JWT_Token>
  ```
- **Response (200 OK)**:
  ```json
  {
    "status": "Healthy",
    "provider": "Microsoft.EntityFrameworkCore.SqlServer",
    "server": "your-server.database.windows.net",
    "database": "PushpakClassesDb",
    "counts": {
      "users": 1,
      "quizzes": 5,
      "categories": 3,
      "attempts": 12
    },
    "timestampUtc": "2026-11-17T08:15:00Z"
  }
  ```
No passwords or credentials are ever exposed in this response or in the logs.

---

## 5. How to Confirm Data Survives a Render Restart

To prove that quizzes, users, and categories survive Render container restarts:

1. **Log in as Admin** on the frontend or via Swagger and create a test Quiz or Category.
2. Call `GET /health/db` or check the record count.
3. In Render Dashboard, go to `pushpakclasses-api`, click **Manual Deploy** -> **Restart service**.
4. Wait for the service to restart (approx 30–60 seconds).
5. Review the new startup logs:
   - Verify `[PERSISTENCE AUDIT]` prints the exact same (or increased) row counts.
6. Call `GET /health/db` or log in through the frontend:
   - Verify all users, quizzes, categories, and test attempts are completely intact.
