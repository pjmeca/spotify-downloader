# Advanced

## spotDL options

The container builds a spotDL command for each download and appends any extra flags you provide via `OPTIONS`.

### How it is assembled

The command includes:

- `download <url>`
- `--format <FORMAT>`
- `--threads <process thread count>`
- `--client-id` and `--client-secret` (from your environment)

If `OPTIONS` does not include `--bitrate`, the container adds `--bitrate disable` by default.

### Custom flags

Set `OPTIONS` to any valid spotDL CLI flags. The best reference is the official spotDL documentation:

https://spotdl.readthedocs.io

Keep credentials out of `OPTIONS`, since they are already injected by the container.

### Example

```yaml
environment:
  FORMAT: "mp3"
  OPTIONS: "--bitrate 320k"
```

If you specify an option that conflicts with the built-in flags, spotDL will apply the last one it sees.

## Build the image locally

If you want to build locally (for development or customization), use the Dockerfile in `SpotifyDownloader/`.

### Build command

```bash
docker build -t spotify-downloader:local ./SpotifyDownloader
```

Then reference it in your compose file:

```yaml
services:
  spotify-downloader:
    image: spotify-downloader:local
```

### What is inside

The image includes:

- .NET runtime (from the official ASP.NET base image)
- ffmpeg
- yt-dlp
- spotDL

Pinned versions are defined in `SpotifyDownloader/Dockerfile`. Update that file if you need different versions.
