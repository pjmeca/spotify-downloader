# Configuration
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
LABEL org.label-schema.name="pjmeca/spotify-downloader" \
    org.label-schema.description="This Docker image periodically tracks and downloads new music for your library using spotDL." \
    org.label-schema.url="https://hub.docker.com/r/pjmeca/spotify-downloader" \
    org.label-schema.vcs-url="https://github.com/pjmeca/spotify-downloader" \
    org.label-schema.version="1.1.2" \
    org.label-schema.schema-version="1.0.0-rc.1" \
    org.label-schema.docker.cmd="docker run --name spotify-downloader -v /your/main/music/path:/music -v /path/to/tracking.yaml:/app/tracking.yaml:ro -v /etc/localtime:/etc/localtime:ro -e CRON_SCHEDULE=\"0 0 * * *\" --restart unless-stopped pjmeca/spotify-downloader:latest" \
    maintainer="pjmeca"
USER app
WORKDIR /app

FROM base AS spotdl
USER root
RUN apt-get update && apt-get install -y --no-install-recommends python3-pip python3-venv && \
    apt-get clean && rm -rf /var/lib/apt/lists/*
RUN python3 -m venv my-venv && \
    my-venv/bin/pip install spotdl==4.2.8
RUN my-venv/bin/spotdl --download-ffmpeg
USER app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/SpotifyDownloader"
COPY ["./SpotifyDownloader.csproj", "./"]
RUN dotnet restore "./SpotifyDownloader.csproj"
COPY . .
RUN dotnet build "./SpotifyDownloader.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SpotifyDownloader.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM spotdl AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SpotifyDownloader.dll"]