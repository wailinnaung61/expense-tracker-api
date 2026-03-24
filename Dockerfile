# ============================================
# PRODUCTION DOCKERFILE
# Multi-stage build for .NET 8 API
# ============================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# ------------------------------------------------
# 1️⃣ Copy ALL csproj files (restore cache layer)
# ------------------------------------------------
COPY 01_Presentation.csproj ./
COPY Applications/02_Application.csproj Applications/
COPY Domains/03_Domain.csproj Domains/
COPY Infrastructures/04_Infrastructure.csproj Infrastructures/

# ------------------------------------------------
# 2️⃣ Restore (restores referenced projects)
# ------------------------------------------------
RUN dotnet restore 01_Presentation.csproj \
    --runtime linux-musl-x64

# ------------------------------------------------
# 3️⃣ Copy full source code
# ------------------------------------------------
COPY . .

# ------------------------------------------------
# 4️⃣ Build
# ------------------------------------------------
RUN dotnet build 01_Presentation.csproj \
    -c Release \
    --no-restore \
    --runtime linux-musl-x64

# ============================================
# Stage 2: Publish
# ============================================
FROM build AS publish

RUN dotnet publish 01_Presentation.csproj \
    -c Release \
    --no-build \
    --runtime linux-musl-x64 \
    --self-contained false \
    -o /app/publish \
    /p:UseAppHost=false

# ============================================
# Stage 3: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

RUN apk add --no-cache curl

# Non-root user
RUN addgroup -g 1000 appgroup && \
    adduser -u 1000 -G appgroup -s /bin/sh -D appuser

WORKDIR /app

COPY --from=publish /app/publish .

RUN chown -R appuser:appgroup /app
USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true \
    TZ=UTC

HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "01_Presentation.dll"]
