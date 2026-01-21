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
| `TZ` | no | container default | Time zone for scheduling. |
| `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT` | no | none | Set to `Development` to run immediately on startup. |

## Volumes and paths

| Container path | Required | Purpose |
| --- | --- | --- |
| `/music` | yes | Download destination. |
| `/app/tracking.yaml` | yes | Tracking config (read-only). |
| `/app/cache` | no | SQLite cache to persist state. |
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
