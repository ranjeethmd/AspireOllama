FROM ollama/ollama:latest AS init
# This container connects to the ollama service to pull models, then exits.
# It uses the ollama CLI directly (no curl needed).
ENTRYPOINT ["sh", "-c"]
CMD ["ollama pull qwen3:32b && ollama pull qwen2.5vl:32b && ollama pull nomic-embed-text"]
