FROM alpine:3.12 AS download-hcron
ARG HCRON_URL=https://github.com/lnquy/cron/releases/download/v1.0.1/hcron_1.0.1_linux_x86_64.tar.gz
RUN apk add --no-cache curl tar && \
    curl -L $HCRON_URL -o /tmp/hcron.tar.gz && \
    tar -xzf /tmp/hcron.tar.gz -C /usr/local/bin && \
    rm /tmp/hcron.tar.gz



FROM python:3.12-slim

RUN pip install psutil pyyaml spotdl && \
    spotdl --download-ffmpeg
RUN apt-get update && \
    apt-get install -y cron
WORKDIR /app

ENV CRON_SCHEDULE="0 0 */1 * *"
COPY --from=download-hcron /usr/local/bin/hcron .

ADD automated_run.py .
ADD tracking.yaml .

COPY main.sh start.sh ./
RUN chmod +x main.sh start.sh

WORKDIR /music

ENV FORMAT=opus
ENV OPTIONS=

ENTRYPOINT ["/app/start.sh"]

#ENTRYPOINT [ "python", "/app/automated_run.py", "/app/tracking.yaml" ]