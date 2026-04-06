# ============================================
# Deploy to AWS ECR — Expense Tracker API
# ============================================
# Requires: AWS CLI, Docker Desktop
# Usage: .\deploy-ecr.ps1
# ============================================

$ECR_REGISTRY = "908027401522.dkr.ecr.us-east-1.amazonaws.com"
$ECR_REPO      = "expense_tracker/api"
$IMAGE_TAG     = "latest"
$FULL_IMAGE    = "$ECR_REGISTRY/${ECR_REPO}:${IMAGE_TAG}"
$AWS_REGION    = "us-east-1"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Deploying to AWS ECR" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Login to ECR (using AWS CLI)
Write-Host "`n[1/4] Logging in to ECR..." -ForegroundColor Yellow
aws ecr get-login-password --region $AWS_REGION | docker login --username AWS --password-stdin $ECR_REGISTRY
if ($LASTEXITCODE -ne 0) { Write-Host "ECR login failed!" -ForegroundColor Red; exit 1 }

# 2. Build
Write-Host "`n[2/4] Building Docker image..." -ForegroundColor Yellow
docker build -t "${ECR_REPO}:${IMAGE_TAG}" .
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed!" -ForegroundColor Red; exit 1 }

# 3. Tag
Write-Host "`n[3/4] Tagging image..." -ForegroundColor Yellow
docker tag "${ECR_REPO}:${IMAGE_TAG}" $FULL_IMAGE

# 4. Push
Write-Host "`n[4/4] Pushing to ECR..." -ForegroundColor Yellow
docker push $FULL_IMAGE
if ($LASTEXITCODE -ne 0) { Write-Host "Push failed!" -ForegroundColor Red; exit 1 }

Write-Host "`n========================================" -ForegroundColor Green
Write-Host " Deployed: $FULL_IMAGE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
