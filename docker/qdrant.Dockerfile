FROM qdrant/qdrant:latest
RUN apt-get update -qq && apt-get install -y -qq curl > /dev/null && rm -rf /var/lib/apt/lists/*
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=15s \
    CMD curl -f http://localhost:6333/healthz || exit 1
