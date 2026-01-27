# Getting Started

This guide walks you from zero to a running container.

## Prerequisites

- [Docker and Docker Compose](https://docs.docker.com/engine/install/)
- A place on your host where the music will be stored

You will create Spotify credentials and a `tracking.yaml` file in the next steps.

## 1) Create Spotify credentials

Spotify Downloader uses Spotify **client credentials** to read artist and playlist data. You need a Client ID and Client Secret so the app can call the Spotify API.

Go to the Spotify Developer Dashboard and create an app:

<https://developer.spotify.com/dashboard>

Copy the **Client ID** and **Client Secret**. These values go into `CLIENT__ID` and `CLIENT__SECRET` in your compose file.

Note: client credentials only allow access to **public** playlists.

## 2) Create a tracking file

Create a `tracking.yaml` file on your host:

```yaml
artists:
  - name: Dua Lipa
    url: https://open.spotify.com/artist/6M2wZ9GZgrQXHCFfjv46we
  - name: The Beatles
    url: https://open.spotify.com/artist/3WrFJ7ztbogyGnTHbHJFl2
    refresh: false

playlists:
  - name: Chill Mix
    url: https://open.spotify.com/playlist/37i9dQZF1DWXRqgorJj26U
    mode: full
```

See the full format and options in [tracking.yaml](configuration.md#trackingyaml).

## 3) Run with Docker Compose

Use a compose file like this:

```yaml
name: spotify-downloader

services:
  spotify-downloader:
    image: pjmeca/spotify-downloader:latest
    container_name: spotify-downloader
    restart: unless-stopped
    volumes:
      - /your/main/music/path:/music
      - /path/to/tracking.yaml:/app/tracking.yaml:ro
      - /path/to/cache:/app/cache
      - /path/to/logs:/app/logs
    environment:
      CRON_SCHEDULE: "0 0 * * *"
      CLIENT__ID: "your_spotify_client_id"
      CLIENT__SECRET: "your_spotify_client_secret"
      TZ: Europe/Madrid
      FORMAT: "opus"
      OPTIONS: ""
```

Then start the container:

```bash
docker compose up -d
```

If you want to build the image locally instead of pulling from Docker Hub, see [Advanced](advanced.md).
