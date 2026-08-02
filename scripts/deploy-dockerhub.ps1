# Build and push API image to Docker Hub.
#
# Prerequisites:
#   1. Docker Desktop running
#   2. .env has DOCKERHUB_USER=your-dockerhub-username
#   3. Logged in:  docker login
#
# Usage (repo root):
#   .\scripts\deploy-dockerhub.ps1
#   .\scripts\deploy-dockerhub.ps1 -Tag v1

param(
    [string]$Tag = "latest"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

$envFile = Join-Path $Root ".env"
if (-not (Test-Path $envFile)) {
    throw "Missing .env — copy .env.example to .env and set DOCKERHUB_USER"
}

$user = $null
Get-Content $envFile | ForEach-Object {
    if ($_ -match '^\s*DOCKERHUB_USER\s*=\s*(.+)\s*$') {
        $user = $Matches[1].Trim().Trim('"').Trim("'")
    }
}

if ([string]::IsNullOrWhiteSpace($user) -or $user -eq "your-dockerhub-username") {
    throw "Set DOCKERHUB_USER in .env to your Docker Hub username (not the placeholder)."
}

$image = "${user}/expense-tracker-api:${Tag}"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Docker Hub: $image" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`n[1/3] docker login (use Docker Hub username/password or token)..." -ForegroundColor Yellow
docker login
if ($LASTEXITCODE -ne 0) { throw "docker login failed" }

Write-Host "`n[2/3] Building $image ..." -ForegroundColor Yellow
docker build -t $image .
if ($LASTEXITCODE -ne 0) { throw "docker build failed" }

Write-Host "`n[3/3] Pushing $image ..." -ForegroundColor Yellow
docker push $image
if ($LASTEXITCODE -ne 0) { throw "docker push failed" }

Write-Host "`nDone. On another PC:" -ForegroundColor Green
Write-Host "  1) Same .env (including DOCKERHUB_USER=$user)"
Write-Host "  2) docker compose pull api"
Write-Host "  3) docker compose up -d"
Write-Host ""
Write-Host "Image: https://hub.docker.com/r/$user/expense-tracker-api" -ForegroundColor Green
