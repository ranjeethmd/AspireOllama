FROM redis:7-alpine
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=15s \
    CMD redis-cli ping | grep PONG || exit 1
EXPOSE 6379
