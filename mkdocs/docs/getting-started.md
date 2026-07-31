---
title: Run SpotCrate with Docker Compose
description: Create Spotify credentials, define tracked artists and playlists, and start SpotCrate with Docker Compose.
---

# Getting Started

This guide walks you from zero to a running container.

## Prerequisites

- [Docker and Docker Compose](https://docs.docker.com/engine/install/)
- A place on your host where the music will be stored

You will create Spotify credentials and a `tracking.yaml` file in the next steps.

## 1) Create Spotify credentials

SpotCrate uses Spotify **client credentials** to read artist and playlist data. You need a Client ID and Client Secret so the app can call the Spotify API.

Go to the Spotify Developer Dashboard and create an app:

<https://developer.spotify.com/dashboard>

Copy the **Client ID** and **Client Secret**. These values go into `CLIENT__ID` and `CLIENT__SECRET` in your compose file.

Note: client credentials only allow access to **public** playlists.

???+ warning "Spotify Developer policy changes (March 9, 2026)"
    Spotify now requires the **app owner** to have an **active Spotify Premium subscription** for Development Mode Web API access. If the app owner does not have Premium, this project may stop working.

    Spotify also introduced stricter Development Mode limits (for example, fewer supported endpoints/fields, one Client ID per developer, and up to five authorized users). These limits can affect functionality depending on your setup.

    See the full discussion in **[#51](https://github.com/pjmeca/spotcrate/issues/51)**.

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
name: spotcrate

services:
  spotcrate:
    image: pjmeca/spotcrate:latest
    container_name: spotcrate
    restart: unless-stopped
    ports:
      - "127.0.0.1:8080:8080" # Optional web UI
    volumes:
      - /your/main/music/path:/music
      - /path/to/tracking.yaml:/app/tracking.yaml
      - /path/to/cache:/app/cache
      - /path/to/logs:/app/logs
    environment:
      CRON_SCHEDULE: "0 0 * * *"
      CLIENT__ID: "your_spotify_client_id"
      CLIENT__SECRET: "your_spotify_client_secret"
      TZ: Europe/Madrid
      FORMAT: "opus"
      OPTIONS: ""
      YT_DLP_UPDATE_POLICY: "before-run"
```

Mount `tracking.yaml` without `:ro` if you want to use the web UI to save edits. If you only edit the file manually, you can keep using `/path/to/tracking.yaml:/app/tracking.yaml:ro`.

Then start the container:

```bash
docker compose up -d
```

Open <http://localhost:8080> to use the optional web UI, or continue editing `tracking.yaml` manually. The UI can add, edit, delete, rename, sort, and reorder entries when `tracking.yaml` is mounted writable.

If you want to build the image locally instead of pulling from Docker Hub, see [Advanced](advanced.md).
