# Multi-stage Dockerfile for all AspireOllama .NET services
# Usage: docker build --build-arg PROJECT=AspireOllama.Web -t aspireollama-web .

ARG PROJECT

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

# Install New Relic .NET agent
RUN apt-get update && apt-get install -y wget ca-certificates gnupg \
&& echo 'deb http://apt.newrelic.com/debian/ newrelic non-free' | tee /etc/apt/sources.list.d/newrelic.list \
&& wget https://download.newrelic.com/548C16BF.gpg \
&& apt-key add 548C16BF.gpg \
&& apt-get update \
&& apt-get install -y 'newrelic-dotnet-agent' \
&& rm -rf /var/lib/apt/lists/*

# Enable New Relic .NET agent
ENV CORECLR_ENABLE_PROFILING=1 \
    CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
    CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
    CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
    NEW_RELIC_LICENSE_KEY=${NEW_RELIC_LICENSE_KEY} \
    NEW_RELIC_APP_NAME="AspireOllama"

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
ARG PROJECT
WORKDIR /src

# Copy solution and project files for restore
COPY AspireOllama.slnx .
COPY AspireOllama.ServiceDefaults/*.csproj AspireOllama.ServiceDefaults/
COPY AspireOllama.Shared/*.csproj AspireOllama.Shared/
COPY AspireOllama.Web/*.csproj AspireOllama.Web/
COPY AspireOllama.ApiService/*.csproj AspireOllama.ApiService/
COPY AspireOllama.McpServer/*.csproj AspireOllama.McpServer/
COPY AspireOllama.Gateway/*.csproj AspireOllama.Gateway/
COPY A2A/AspireOllama.A2A.Shared/*.csproj A2A/AspireOllama.A2A.Shared/
COPY A2A/AspireOllama.A2A.PlannerAgent/*.csproj A2A/AspireOllama.A2A.PlannerAgent/
COPY A2A/AspireOllama.A2A.ReviewerAgent/*.csproj A2A/AspireOllama.A2A.ReviewerAgent/
COPY A2A/AspireOllama.A2A.ResearchAgent/*.csproj A2A/AspireOllama.A2A.ResearchAgent/
COPY A2A/AspireOllama.A2A.CodeAgent/*.csproj A2A/AspireOllama.A2A.CodeAgent/
COPY AspireOllama.AppHost/*.csproj AspireOllama.AppHost/

RUN dotnet restore ${PROJECT}/${PROJECT}.csproj

# Copy source and publish
COPY . .
RUN dotnet publish ${PROJECT}/${PROJECT}.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG PROJECT
ENV ASPNETCORE_URLS=http://+:8080
WORKDIR /app
COPY --from=build /app/publish .

# Determine entry point DLL from project name
ENV APP_DLL=${PROJECT}.dll
ENTRYPOINT ["sh", "-c", "dotnet $APP_DLL"]
