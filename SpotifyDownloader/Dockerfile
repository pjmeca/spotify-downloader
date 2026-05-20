# Configuration
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
LABEL org.label-schema.name="pjmeca/spotify-downloader" \
    org.label-schema.description="This Docker image periodically tracks and downloads new music for your library using spotDL." \
    org.label-schema.url="https://hub.docker.com/r/pjmeca/spotify-downloader" \
    org.label-schema.vcs-url="https://github.com/pjmeca/spotify-downloader" \
    org.label-schema.version="2.6.1" \
    org.label-schema.schema-version="1.0.0-rc.1" \
    maintainer="pjmeca"
WORKDIR /
USER root
# Install dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    python3-pip \
    python3-venv \
    ca-certificates \
    curl \
    unzip && \
    curl -fsSL https://deno.land/install.sh | sh && \
    ln -s /root/.deno/bin/deno /usr/local/bin/deno && \
    apt-get purge -y unzip && \
    apt-get autoremove -y && \
    apt-get clean && rm -rf /var/lib/apt/lists/*
# Install yt-dlp and spotdl
RUN python3 -m venv env && \
    /env/bin/pip install --no-cache-dir \
        yt-dlp==2026.03.17 \
        spotdl==4.5.0
# Configure yt-dlp to use the GitHub EJS extractor
RUN echo "--remote-components=ejs:github" > /etc/yt-dlp.conf
WORKDIR /app
ENV CRON_SCHEDULE=
ENV CLIENT__ID=
ENV CLIENT__SECRET=
ENV FORMAT="opus"
ENV OPTIONS=
ENV YT_DLP_UPDATE_POLICY="before-run"

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/SpotifyDownloader"
COPY ["./SpotifyDownloader.csproj", "./"]
RUN dotnet restore "./SpotifyDownloader.csproj"
COPY . .
RUN dotnet build "./SpotifyDownloader.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SpotifyDownloader.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SpotifyDownloader.dll"]
