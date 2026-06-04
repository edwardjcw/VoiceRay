# VoiceRay MFA worker — Phase 4 stub (KAN-55)
# Replace with conda/MFA base image when implementing real alignment.
FROM python:3.11-slim-bookworm

WORKDIR /app

RUN pip install --no-cache-dir fastapi uvicorn[standard]

COPY stub_server.py healthcheck.sh ./
RUN chmod +x healthcheck.sh

EXPOSE 8765

HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
  CMD ["./healthcheck.sh"]

CMD ["uvicorn", "stub_server:app", "--host", "0.0.0.0", "--port", "8765"]
