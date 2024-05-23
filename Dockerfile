FROM alpine:3.12 AS download-hcron
ARG HCRON_URL=https://github.com/lnquy/cron/releases/download/v1.0.1/hcron_1.0.1_linux_x86_64.tar.gz
RUN apk add --no-cache curl tar && \
    curl -L $HCRON_URL -o /tmp/hcron.tar.gz && \
    tar -xzf /tmp/hcron.tar.gz -C /usr/local/bin && \
    rm /tmp/hcron.tar.gz

FROM python:3.12-slim
RUN apt-get update && apt-get install -y --no-install-recommends cron && \
    apt-get clean && rm -rf /var/lib/apt/lists/*
RUN pip install --no-cache-dir psutil pyyaml spotdl && \
    spotdl --download-ffmpeg
WORKDIR /app
COPY --from=download-hcron /usr/local/bin/hcron .
COPY automated_run.py main.sh start.sh tracking.yaml ./
RUN chmod +x main.sh start.sh
WORKDIR /music
ENV CRON_SCHEDULE="0 0 * * *"
ENV FORMAT=opus
ENV OPTIONS=
ENTRYPOINT ["/app/start.sh"]