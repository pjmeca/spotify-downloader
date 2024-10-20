# Configuration
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
LABEL org.label-schema.name="pjmeca/spotify-downloader" \
    org.label-schema.description="This Docker image periodically tracks and downloads new music for your library using spotDL." \
    org.label-schema.url="https://hub.docker.com/r/pjmeca/spotify-downloader" \
    org.label-schema.vcs-url="https://github.com/pjmeca/spotify-downloader" \
    org.label-schema.version="2.1.1" \
    org.label-schema.schema-version="1.0.0-rc.1" \
    maintainer="pjmeca"
WORKDIR /
USER root
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg python3-pip python3-venv && \
    apt-get clean && rm -rf /var/lib/apt/lists/*
RUN python3 -m venv env && \
    /env/bin/pip install spotdl==4.2.8
WORKDIR /app
ENV CRON_SCHEDULE=
ENV CLIENT__ID=
ENV CLIENT__SECRET=
ENV FORMAT="opus"
ENV OPTIONS=

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

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SpotifyDownloader.dll"]