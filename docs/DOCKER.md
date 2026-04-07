# 🐳 Docker Commands — Expense Tracker API

## Quick Start

```powershell
# Start everything (API + PostgreSQL + Redis)
docker-compose up --build -d
```

API: `http://localhost:80`

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

## Deploy New Code Changes (Local)

```powershell
# Rebuild API image and restart (keeps DB data)
docker-compose up --build -d
docker-compose pull api && docker-compose up -d api

```

---

## Production Deployment (AWS ECR → EC2)

### Architecture

```
[Your PC] → deploy-ecr.ps1 → [AWS ECR] → docker pull → [EC2 Instance]
                                                            ├── API (from ECR)
                                                            ├── PostgreSQL (Docker Hub)
                                                            └── Redis (Docker Hub)
```

> **Never clone source code to EC2.** The Docker image from ECR has everything built inside.

### Step 1 — Build & Push to ECR (on your PC)

**Windows:**
```powershell
.\deployment\deploy-ecr.ps1
```

**Linux/Mac:**
```sh
chmod +x deployment/deploy-ecr.sh
./deployment/deploy-ecr.sh
```

### Step 2 — Pull & Restart on EC2

```sh
# SSH into EC2
ssh -i your-key.pem ec2-user@your-ec2-ip

# Go to project directory
cd ~/expensetracker

# Login to ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 908027401522.dkr.ecr.us-east-1.amazonaws.com

# Pull latest API image & restart (DB + Redis untouched)
docker-compose pull api
docker-compose up -d api
```

### When to Restart What

| Change | Command on EC2 |
|---|---|
| API code change | `docker-compose pull api && docker-compose up -d api` |
| `.env` change | `docker-compose up -d api` |
| DB password change | `docker-compose up -d` |
| First time setup | `docker-compose up -d` |
| Nuclear reset | `docker-compose down -v && docker-compose up -d` |

### Production Logs (on EC2)

```sh
# API logs
docker-compose logs -f api

# All logs
docker-compose logs -f

# Last 100 lines
docker-compose logs --tail=100 api
```

---

## Deployment Files

```
deployment/
├── .env.example              # Environment variables template → copy to .env
├── deploy-ecr.ps1            # Push to ECR (Windows)
├── deploy-ecr.sh             # Push to ECR (Linux)

.github/workflows/
└── deploy-ecr.yml            # CI/CD: auto build → ECR → approve → deploy EC2

docker-compose.yml            # Used on both local & EC2
Dockerfile                    # Multi-stage build
```

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
docker exec -it expense-tracker-redis redis-cli -a your_redis_password

#redis command
AUTH your_redis_password
KEY *

# Flush all Redis cache
docker exec expense-tracker-redis redis-cli -a your_redis_password FLUSHALL
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

| Service | Container | Local Port | Prod Port | Internal Host |
|---------|-----------|-----------|-----------|---------------|
| API | `expense-tracker-api` | `8080` | `80` | `api:8080` |
| PostgreSQL | `expense-tracker-db` | `5432` | `5432` | `postgres:5432` |
| Redis | `expense-tracker-redis` | `6379` | `6379` | `redis:6379` |
| RedisInsight | `expense-tracker-redisinsight` | — | `5540` | — |

---

## Environment Variables (Override)

```powershell
# Override AWS settings
docker-compose up -d -e AWS__AccessKey=xxx -e AWS__SecretKey=xxx

# Or edit docker-compose.yml → api → environment section
```

### Key environment variables for API:

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Environment |
| `ConnectionStrings__DefaultConnection` | `Host=postgres;...` | PostgreSQL |
| `ConnectionStrings__Redis` | `redis:6379,...` | Redis |
| `AWS__Region` | — | AWS region |
| `AWS__AccessKey` | — | AWS access key |
| `AWS__SecretKey` | — | AWS secret key |
| `AWS__Cognito__UserPoolId` | — | Cognito pool |
| `AWS__Cognito__ClientId` | — | Cognito client |
| `CorsSettings__AllowedOrigins__0` | — | Frontend URL |


## Update DockerCompose

```update api
# api only
docker-compose up -d --force-recreate api

# update all services
docker-compose up -d --force-recreate
```
