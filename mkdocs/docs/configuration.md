---
title: Configure SpotCrate
description: Configure SpotCrate environment variables, Docker volumes, the optional web UI, and tracking.yaml.
---

# Configuration

Everything you need to configure and run the container, without the noise.

## Environment variables

| Variable | Required | Default | Description |
| --- | --- | --- | --- |
| `CRON_SCHEDULE` | yes | none | Cron expression in 5-field format. |
| `CLIENT__ID` | yes | none | Spotify Client ID. |
| `CLIENT__SECRET` | yes | none | Spotify Client Secret. |
| `FORMAT` | no | `opus` | Audio format for spotDL (ffmpeg-compatible). |
| `OPTIONS` | no | empty | Extra spotDL flags. **Do not put credentials here.** |
| `YT_DLP_UPDATE_POLICY` | no | `before-run` | `before-run` updates `yt-dlp` at the start of each scheduled run. `never` keeps the currently installed version. Any other value is treated as a fixed `yt-dlp` version to install before the run. |
| `TZ` | no | container default | Time zone for scheduling. |
| `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT` | no | none | Set to `Development` to run immediately on startup. |

## Volumes and paths

| Container path | Required | Purpose |
| --- | --- | --- |
| `/music` | yes | Download destination. |
| `/app/tracking.yaml` | yes | Tracking config. Use read-only for manual editing, or writable when using the web UI to save changes. |
| `/app/cache` | no | SQLite cache and ASP.NET Data Protection keys. Persist this path to keep web UI antiforgery tokens valid across container recreations. |
| `/app/logs` | no | Log files. |

On disk, files are organized like this:

```text
/music
  /Artists
    /Artist Name
      /Album Name
        track.ext
  /Playlists
    /Playlist Name
      track.ext
```

## Web UI

The app serves a Razor Pages web UI on port `8080`. Publish the port in Docker Compose, then open `http://localhost:8080` to manage tracked artists and playlists. The UI validates that names are present, that URLs use `http` or `https`, and that artist and playlist entries point to matching Spotify artist or playlist URLs before persisting changes back to `tracking.yaml` when the file is writable.

The editor can:

- Add, edit, delete, and rename tracked artists and playlists.
- Configure `refresh` for artists and playlists.
- Configure playlist `mode` as `add` or `full`.
- Rename the existing local folder when an entry name changes, when the folder can be found and moved safely.
- Show entries in `A-Z` order without changing `tracking.yaml`.
- Show entries in `YAML` order and reorder them with drag-and-drop; YAML-order changes are saved immediately.
- Remember the selected order mode in the browser.
- Keep the current page position after form submissions.
- Show validation and save errors without adding success messages that shift the page layout.
- Adapt the layout for mobile screens, including compact status, documentation, and GitHub links.

<p align="center">
  <img src="/assets/images/webui-landing.webp" alt="Desktop web UI example"/>
</p>
<p align="center">
  <img src="/assets/images/webui-dialog.webp" alt="Desktop web UI dialog example"/>
</p>

If `tracking.yaml` does not appear writable, the page remains available as a read-only view and shows a warning. To enable UI edits in Docker, mount `/app/tracking.yaml` without `:ro`:

```yaml
ports:
  - "127.0.0.1:8080:8080"
volumes:
  - /path/to/tracking.yaml:/app/tracking.yaml
```

If you do not want UI-based editing, you can keep the existing read-only mount:

```yaml
volumes:
  - /path/to/tracking.yaml:/app/tracking.yaml:ro
```

## tracking.yaml

Define what to download. The file is read on every run.

### Top-level keys

| Key | Required | Type | Description |
| --- | --- | --- | --- |
| `artists` | no | list | Artists to track. |
| `playlists` | no | list | Playlists to track. |

### Artist entry

| Field | Required | Type | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | yes | string | none | Folder name under `/music/Artists`. |
| `url` | yes | string | none | Spotify artist URL. |
| `refresh` | no | boolean | `true` | If `false`, skip scanning if the artist already exists locally. |

### Playlist entry

| Field | Required | Type | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | yes | string | none | Folder name under `/music/Playlists`. |
| `url` | yes | string | none | Spotify playlist URL (public only). |
| `refresh` | no | boolean | `true` | If `false`, skip scanning if the playlist already exists locally. |
| `mode` | no | string | `add` | `add` downloads only new tracks. `full` syncs the folder and deletes local tracks no longer present remotely. |

For compatibility with older `tracking.yaml` files, playlists without `mode` keep using `add`. New playlists created from the web UI may explicitly set a mode when saved.

### Example

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
    mode: add
```

## How downloads behave

### Artists

- New albums and singles are detected and downloaded.
- `compilation` releases are skipped.
- Album folders are created only when multiple tracks share the same album.

### Playlists

- `add` mode only downloads new tracks.
- `full` mode syncs the local folder and removes tracks that are no longer in the remote playlist.

## Smart Deletion

The app stores downloaded **artist albums** in a SQLite cache. If you delete a track or album you do not like, it will not be downloaded again because the album is already marked as downloaded.

This applies to artist tracking only. For playlists, missing tracks will be downloaded again in `add` mode.

If you want to force a re-download for artists, remove the cache (`/app/cache`) and run again.
