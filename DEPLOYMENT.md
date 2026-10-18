# Deployment Guide

## Architecture

- **Frontend:** Next.js on Vercel (free tier)
- **Backend:** .NET 10 API on Azure Container Apps (free tier: 180K vCPU-sec/mo, 2M requests/mo)
- **Database:** Azure SQL Database serverless free tier (GP_S_Gen5_1, auto-pause after 60min idle, 32GB)
- **Caching:** In-memory (no Redis in production to stay free)
- **Container Registry:** GitHub Container Registry (ghcr.io, free for public repos)

**Target cost: $0/month** within free tier limits.

---

## Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- [GitHub account](https://github.com) with the repository
- [Vercel account](https://vercel.com) (free tier)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for local development)
- [Node.js 22+](https://nodejs.org/) (for frontend development)
- [Docker](https://www.docker.com/) (for local container builds)

---

## 1. Azure Service Principal Setup

Create a service principal for GitHub Actions to deploy to Azure:

```bash
az login
az account set --subscription <YOUR_SUBSCRIPTION_ID>

az ad sp create-for-rbac \
  --name "ffxiv-crafting-github" \
  --role contributor \
  --scopes /subscriptions/<YOUR_SUBSCRIPTION_ID> \
  --sdk-auth
```

Save the JSON output — this is your `AZURE_CREDENTIALS` secret.

---

## 2. GitHub Secrets Configuration

Add these secrets in **Settings > Secrets and variables > Actions**:

| Secret | Description |
|--------|-------------|
| `AZURE_CREDENTIALS` | Service principal JSON from step 1 |
| `AZURE_RESOURCE_GROUP` | Resource group name (e.g., `rg-ffxiv-crafting`) |
| `SQL_ADMIN_PASSWORD` | Strong password for Azure SQL admin |
| `SQL_CONNECTION_STRING` | The API's connection string; the deploy workflow's `migrate` job also uses it to run EF Core migrations |
| `JWT_SECRET_KEY` | 32+ byte secret key for JWT signing |
| `ADMIN_API_KEY` | Value of the `X-Admin-Key` header the admin endpoints require (`/api/admin/*`, `/health`) |
| `CORS_ORIGINS` | The site's origin (e.g., `https://ffxivcrafting.app`) |
| `DISCORD_CLIENT_ID` | Discord OAuth application client ID |
| `DISCORD_CLIENT_SECRET` | Discord OAuth application client secret |
| `DISCORD_REDIRECT_URI` | Discord callback URL (e.g., `https://ffxivcrafting.app/auth/discord/callback`) |
| `GHCR_USERNAME`, `GHCR_PASSWORD` | Credentials the Container App pulls the image with (a token with `read:packages`) |

And one repository **variable** (Settings > Secrets and variables > Actions > Variables):

| Variable | Description |
|----------|-------------|
| `API_BASE_URL` | The API origin; the Data Export workflow posts `/api/admin/sync` there after publishing a new data set |

---

## 3. First-Time Infrastructure Deployment

### Option A: GitHub Actions (recommended)

1. Go to **Actions > Infrastructure Deploy**
2. Click **Run workflow**
3. Set location (e.g., `eastus`) and environment (`prod`)
4. Wait for deployment to complete
5. Note the API URL from the output

### Option B: Local deployment

```powershell
# Set environment variables
$env:SQL_ADMIN_PASSWORD = "YourStrongPassword123!"
$env:JWT_SECRET_KEY = "your-32-byte-secret-key-here-minimum!!"
$env:CORS_ORIGINS = "https://your-app.vercel.app"
$env:DISCORD_CLIENT_ID = ""
$env:DISCORD_CLIENT_SECRET = ""
$env:DISCORD_REDIRECT_URI = ""
$env:CONTAINER_IMAGE = "ghcr.io/YOUR_USERNAME/ffxiv-crafting-api:latest"

./infra/scripts/deploy.ps1 -ResourceGroup "rg-ffxiv-crafting" -Location "eastus"
```

---

## 4. Backend Deployment

Backend deploys automatically when you push changes to `src/**` or `Dockerfile` on `main`:

1. GitHub Actions runs tests
2. Builds and pushes Docker image to ghcr.io
3. Updates Azure Container App with new image

For manual deployment:
```bash
# Build and push image
docker build -t ghcr.io/YOUR_USERNAME/ffxiv-crafting-api:latest .
docker push ghcr.io/YOUR_USERNAME/ffxiv-crafting-api:latest

# Update container app
az containerapp update \
  --name ffxiv-prod-api \
  --resource-group rg-ffxiv-crafting \
  --image ghcr.io/YOUR_USERNAME/ffxiv-crafting-api:latest
```

---

## 5. Vercel Setup

1. Go to [vercel.com](https://vercel.com) and import your GitHub repository
2. Set **Root Directory** to `frontend`
3. Framework preset will auto-detect Next.js
4. Add environment variables:
   - `NEXT_PUBLIC_API_URL` = your Azure Container Apps URL (e.g., `https://ffxiv-prod-api.kindgrass-abc123.eastus.azurecontainerapps.io`)
   - `NEXT_PUBLIC_SITE_URL` = the canonical origin (e.g., `https://ffxivcrafting.app`). Required for the Production environment: every canonical URL, sitemap entry and social image is published under it, and a production build fails without it. Requests to the project's `*.vercel.app` host are redirected (308) to this origin by `next.config.ts`.
   - `NEXT_PUBLIC_SENTRY_DSN` (optional) = the Sentry DSN; also the destination for Content-Security-Policy violation reports
   - Game data needs no variable: the build downloads the data set pinned in `frontend/data.lock.json` (a commit of the `data` branch) for its server-side reads, and browsers and on-demand renders read the same commit from jsDelivr (`https://cdn.jsdelivr.net/gh/AdamWyatt34/FFXIVCraftingCompanion@<dataSha>/…`), so the deployment does not carry it. That needs the repository to be public; while it is private, or whenever the CDN cannot serve the locked data set, the build copies it under `public/data/` and serves it itself, which adds about 260 MB to the deployment (Hobby includes 10 GB of Deployment Storage; a CDN-served deployment is about 70 MB). The build log says which happened. `FFXIV_DATA_BASE_URL` points the site at another origin serving the data set with CORS; `FFXIV_DATA_SERVE=local` forces the fallback.
5. Deploy

Vercel will auto-deploy on pushes to `main` and create preview deployments for PRs.

The simulator and solver run in the browser from a prebuilt WASM module committed at `frontend/src/wasm/` (the Vercel build image has no Rust toolchain, so nothing is compiled at deploy time). After any change under `crates/`, run `npm run wasm:build` in `frontend/` and commit the result; the `WasmArtifactTests` test in backend CI fails when the artifact is stale.

---

## 6. Initial Data Sync

After the backend is running, trigger a data sync to populate the database. The admin endpoints
answer 401 unless the `X-Admin-Key` header matches the `Admin:ApiKey` setting (the `ADMIN_API_KEY`
secret in production); with no key configured they are closed.

```bash
curl -X POST https://YOUR_API_URL/api/admin/sync -H "X-Admin-Key: $ADMIN_API_KEY" -H "Content-Length: 0"
```

The sync runs in the background and the call returns at once (a second call while one is running
says so). The scheduled daily sync is off by default (`DataSync:EnableAutoSync`); the Data Export
workflow calls this endpoint once per game patch, which is what keeps the account database's game
tables current.

**Note:** The first sync may take several minutes due to the volume of XIVAPI data.

---

## 7. Cost Monitoring

### Free tier limits
| Service | Free Allowance |
|---------|---------------|
| Azure SQL Database | 1 free database per subscription (auto-pauses) |
| Container Apps | 180K vCPU-sec, 360K GiB-sec, 2M requests/month |
| GitHub Container Registry | Unlimited for public repos |
| Vercel | 100GB bandwidth, 10GB Deployment Storage (retained build output; a deployment is ~70MB with the data set on jsDelivr, ~330MB with the fallback) |

### Monitoring
- **Azure Portal:** Cost Management + Billing > Cost analysis
- **Container Apps:** Monitor > Metrics (request count, CPU, memory)
- **Vercel Dashboard:** Usage tab for bandwidth and function invocations

### Scaling path
| Tier | Estimated Cost | Changes |
|------|---------------|---------|
| Free | $0/mo | Current setup |
| Basic | ~$5/mo | Add Redis Cache Basic ($5), increase Container Apps resources |
| Standard | ~$25/mo | Azure SQL S0 ($15), Redis Basic ($5), Container Apps dedicated |

---

## Local Development Setup

```bash
# Start SQL Server and Redis
docker compose up -d

# Run backend
cd src/FFXIV.Api
dotnet run

# Run frontend (in another terminal)
cd frontend
npm install
npm run dev
```

The backend runs on `http://localhost:5159` and the frontend on `http://localhost:3000`.

### Local database connection
- **Server:** localhost,1433
- **User:** sa
- **Password:** YourStrong!Passw0rd
- **Database:** ffxiv_crafting

---

## Troubleshooting

### Container App not starting
- Check logs: `az containerapp logs show --name ffxiv-prod-api --resource-group rg-ffxiv-crafting`
- Verify the image exists: `docker pull ghcr.io/YOUR_USERNAME/ffxiv-crafting-api:latest`
- Check the liveness endpoint: the app must respond on `/health/live` at port 8080 (`/health`, the full readiness report, needs the `X-Admin-Key` header)

### Database connection issues
- Azure SQL auto-pauses after 60 minutes of inactivity — first request may take ~30 seconds to resume
- Verify firewall rule allows Azure services: `AllowAzureServices` rule should be present
- Check connection string in Container App environment variables

### CORS errors
- Verify `CORS_ORIGINS` secret matches your exact Vercel URL (including `https://`)
- The backend reads allowed origins from `Cors:AllowedOrigins:0` environment variable

### EF migrations not applying
- Migrations run in the **Backend Deploy** workflow's `migrate` job (`dotnet ef database update`
  against `SQL_CONNECTION_STRING`, through a firewall rule opened for the runner), not at
  application start (`ApplyMigrationsOnStartup` is `false` in production)
- If the job failed (for example while the serverless database was paused or its free quota was
  exhausted), re-run the workflow from **Actions > Backend Deploy > Run workflow** once the
  database is back
- For a manual migration: connect to Azure SQL and run `dotnet ef database update` from `src/FFXIV.Api`
