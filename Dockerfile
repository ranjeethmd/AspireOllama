# Multi-stage Dockerfile for all AspireOllama .NET services
# Telemetry: OTEL only (traces, metrics, logs exported via AddServiceDefaults)
# Usage: docker build --build-arg PROJECT_DIR=AspireOllama.Gateway --build-arg PROJECT_NAME=AspireOllama.Gateway .
# A2A:   docker build --build-arg PROJECT_DIR=A2A/AspireOllama.A2A.CoordinatorAgent --build-arg PROJECT_NAME=AspireOllama.A2A.CoordinatorAgent .

ARG PROJECT_DIR
ARG PROJECT_NAME

# ── Runtime base ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Install curl for healthchecks
RUN apt-get update -qq && apt-get install -y -qq curl && rm -rf /var/lib/apt/lists/*

# ── Build stage ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT_DIR
ARG PROJECT_NAME
WORKDIR /src

COPY . .
RUN dotnet publish "${PROJECT_DIR}/${PROJECT_NAME}.csproj" -c Release -o /app/publish

# ── Final image ──
FROM base AS final
ARG PROJECT_NAME
ENV ASPNETCORE_URLS=http://+:8080
WORKDIR /app
COPY --from=build /app/publish .

# Run as non-root user for security
RUN adduser --disabled-password --gecos "" --no-create-home appuser
USER appuser

HEALTHCHECK --interval=30s --timeout=10s --retries=5 --start-period=60s \
    CMD curl -f http://localhost:8080/health || exit 1

ENV APP_DLL="${PROJECT_NAME}.dll"
ENTRYPOINT ["sh", "-c", "dotnet ${APP_DLL}"]
