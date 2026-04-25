# 🐳 Docker Commands — Expense Tracker API

## Quick Start

```powershell
# Start everything (API + PostgreSQL + Redis)
docker-compose up --build -d
```

API: `http://localhost:8080`

---

## Daily Commands

```powershell
# Start all services (background)
docker-compose up -d

# Stop all services
docker-compose down

# Restart all services
docker-compose restart
```

---

## Deploy New Code Changes

```powershell
# Rebuild API image and restart (keeps DB data)
docker-compose up --build -d
```

> EF migrations run automatically on startup — no manual migration needed.

---

## Logs

```powershell
# All logs (follow)
docker-compose logs -f

# API logs only
docker-compose logs -f api

# PostgreSQL logs only
docker-compose logs -f postgres

# Redis logs only
docker-compose logs -f redis

# Last 100 lines of API logs
docker-compose logs --tail=100 api
```

---

## Container Status

```powershell
# List running containers
docker-compose ps

# Health check
curl http://localhost:8080/health
```

---

## Database

```powershell
# Connect to PostgreSQL CLI
docker exec -it expense-tracker-db psql -U postgres -d ExpenseTracker

# Show all tables
docker exec -it expense-tracker-db psql -U postgres -d ExpenseTracker -c "\dt"

# Show table columns
docker exec -it expense-tracker-db psql -U postgres -d ExpenseTracker -c "\d member_profiles"

# Run a query
docker exec -it expense-tracker-db psql -U postgres -d ExpenseTracker -c "SELECT * FROM member_profiles LIMIT 5;"

# Count rows
docker exec -it expense-tracker-db psql -U postgres -d ExpenseTracker -c "SELECT COUNT(*) FROM transactions;"

# Backup database
docker exec expense-tracker-db pg_dump -U postgres ExpenseTracker > backup.sql

# Restore database
docker exec -i expense-tracker-db psql -U postgres -d ExpenseTracker < backup.sql
```

### Useful psql commands (inside CLI)

| Command | Description |
|---------|-------------|
| `\dt` | List all tables |
| `\d table_name` | Show table columns |
| `\di` | List all indexes |
| `\du` | List users/roles |
| `\l` | List databases |
| `\q` | Quit |

---

## Redis

```powershell
# Connect to Redis CLI
docker exec -it expense-tracker-redis redis-cli

# Flush all Redis cache
docker exec expense-tracker-redis redis-cli FLUSHALL
```

---

## Clean Up

```powershell
# Stop and remove containers (keeps data volumes)
docker-compose down

# Stop and remove containers + delete all data (DB + Redis)
docker-compose down -v

# Remove unused images
docker image prune -f

# Full clean rebuild (no cache)
docker-compose build --no-cache
docker-compose up -d
```

---

## Services

| Service | Container | Port | Internal Host |
|---------|-----------|------|---------------|
| API | `expense-tracker-api` | `8080` | `api` |
| PostgreSQL | `expense-tracker-db` | `5432` | `postgres` |
| Redis | `expense-tracker-redis` | `6379` | `redis` |

---

## Environment Variables (Override)

```powershell
# Optional: static IAM user keys (omit both to use the SDK default chain instead)
docker-compose up -d -e AWS__AccessKey=xxx -e AWS__SecretKey=xxx

# Or edit docker-compose.yml → api → environment section
```

**Without access keys:** leave `AWS__AccessKey` and `AWS__SecretKey` unset (or empty in `appsettings`). The API uses the [AWS default credential provider chain](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/creds-assign.html): e.g. **IAM instance profile** on EC2, **task role** on ECS/Fargate, or credentials from `~/.aws/credentials` when developing locally. Attach an IAM role/policy that allows Cognito, S3 (exports), and EventBridge as needed.

### Key environment variables for API:

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Environment |
| `ConnectionStrings__DefaultConnection` | `Host=postgres;...` | PostgreSQL |
| `ConnectionStrings__Redis` | `redis:6379,...` | Redis |
| `AWS__Region` | — | AWS region |
| `AWS__AccessKey` | *(optional)* | Static key; omit with `AWS__SecretKey` to use IAM role / default chain |
| `AWS__SecretKey` | *(optional)* | Static secret; omit with `AWS__AccessKey` to use IAM role / default chain |
| `AWS__Cognito__UserPoolId` | — | Cognito pool |
| `AWS__Cognito__ClientId` | — | Cognito client |
| `CorsSettings__AllowedOrigins__0` | — | Frontend URL |
