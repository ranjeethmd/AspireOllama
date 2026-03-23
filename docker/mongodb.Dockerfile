FROM mongo:7
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=15s \
    CMD mongosh --eval "db.adminCommand('ping')" || exit 1
EXPOSE 27017
