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
COPY 01.Presentation.csproj ./
COPY 02.Application/02.Application.csproj 02.Application/
COPY 03.Domain/03.Domain.csproj 03.Domain/
COPY 04.Infrastructure/04.Infrastructure.csproj 04.Infrastructure/

# ------------------------------------------------
# 2️⃣ Restore (restores referenced projects)
# ------------------------------------------------
RUN dotnet restore 01.Presentation.csproj \
    --runtime linux-musl-x64

# ------------------------------------------------
# 3️⃣ Copy full source code
# ------------------------------------------------
COPY . .

# ------------------------------------------------
# 4️⃣ Publish (build + publish in one step)
# ------------------------------------------------
RUN dotnet publish 01.Presentation.csproj \
    -c Release \
    --runtime linux-musl-x64 \
    --self-contained false \
    -o /app/publish \
    /p:UseAppHost=false

# ============================================
# Stage 2: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

# ICU for localization (en, ja, my) — required since DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
RUN apk add --no-cache icu-libs curl

# Non-root user
RUN addgroup -g 1000 appgroup && \
    adduser -u 1000 -G appgroup -s /bin/sh -D appuser

WORKDIR /app

COPY --from=build /app/publish .

RUN chown -R appuser:appgroup /app
USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=UTC

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "01.Presentation.dll"]
