FROM python:3.12-slim as base
LABEL org.label-schema.name="pjmeca/spotify-downloader" \
    org.label-schema.description="This Docker image periodically tracks and downloads new music for your library using spotDL." \
    org.label-schema.url="https://hub.docker.com/r/pjmeca/spotify-downloader" \
    org.label-schema.vcs-url="https://github.com/pjmeca/spotify-downloader" \
    org.label-schema.version="1.1.2" \
    org.label-schema.schema-version="1.0.0-rc.1" \
    org.label-schema.docker.cmd="docker run --name spotify-downloader -v /your/main/music/path:/music -v /path/to/tracking.yaml:/app/tracking.yaml:ro -v /etc/localtime:/etc/localtime:ro -e CRON_SCHEDULE=\"0 0 * * *\" --restart unless-stopped pjmeca/spotify-downloader:latest" \
    maintainer="pjmeca"
WORKDIR /app
ENV CRON_SCHEDULE="0 0 * * *"
ENV FORMAT=opus
ENV OPTIONS=

FROM alpine:3.12 AS download-hcron
ARG HCRON_URL=https://github.com/lnquy/cron/releases/download/v1.0.1/hcron_1.0.1_linux_x86_64.tar.gz
RUN apk add --no-cache curl tar && \
    curl -L $HCRON_URL -o /tmp/hcron.tar.gz && \
    tar -xzf /tmp/hcron.tar.gz -C /usr/local/bin && \
    rm /tmp/hcron.tar.gz

FROM base
RUN apt-get update && apt-get install -y --no-install-recommends cron && \
    apt-get clean && rm -rf /var/lib/apt/lists/*
RUN pip install --no-cache-dir psutil pyyaml spotdl==4.2.8 && \
    spotdl --download-ffmpeg
COPY --from=download-hcron /usr/local/bin/hcron .
COPY automated_run.py main.sh start.sh tracking.yaml ./
RUN chmod +x main.sh start.sh
ENTRYPOINT ["/app/start.sh"]