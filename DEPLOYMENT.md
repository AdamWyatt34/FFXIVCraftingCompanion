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
| `JWT_SECRET_KEY` | 32+ byte secret key for JWT signing |
| `CORS_ORIGINS` | Vercel production URL (e.g., `https://your-app.vercel.app`) |
| `DISCORD_CLIENT_ID` | Discord OAuth application client ID |
| `DISCORD_CLIENT_SECRET` | Discord OAuth application client secret |
| `DISCORD_REDIRECT_URI` | Discord callback URL (e.g., `https://your-app.vercel.app/auth/discord/callback`) |

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
4. Add environment variable: `NEXT_PUBLIC_API_URL` = your Azure Container Apps URL (e.g., `https://ffxiv-prod-api.kindgrass-abc123.eastus.azurecontainerapps.io`)
5. Deploy

Vercel will auto-deploy on pushes to `main` and create preview deployments for PRs.

---

## 6. Initial Data Sync

After the backend is running, trigger a data sync to populate the database:

```bash
curl -X POST https://YOUR_API_URL/api/admin/sync
```

Or via the admin API if authentication is required.

**Note:** The first sync may take several minutes due to the volume of XIVAPI data. The free-tier database batch size is configured at 50 to be gentle on resources.

---

## 7. Cost Monitoring

### Free tier limits
| Service | Free Allowance |
|---------|---------------|
| Azure SQL Database | 1 free database per subscription (auto-pauses) |
| Container Apps | 180K vCPU-sec, 360K GiB-sec, 2M requests/month |
| GitHub Container Registry | Unlimited for public repos |
| Vercel | 100GB bandwidth, unlimited deployments |

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
- Check health endpoint: the app must respond on `/health` at port 8080

### Database connection issues
- Azure SQL auto-pauses after 60 minutes of inactivity — first request may take ~30 seconds to resume
- Verify firewall rule allows Azure services: `AllowAzureServices` rule should be present
- Check connection string in Container App environment variables

### CORS errors
- Verify `CORS_ORIGINS` secret matches your exact Vercel URL (including `https://`)
- The backend reads allowed origins from `Cors:AllowedOrigins:0` environment variable

### EF migrations not applying
- Check `ApplyMigrationsOnStartup` is `true` in production config
- Review container logs for migration errors
- For manual migration: connect to Azure SQL and run `dotnet ef database update`
