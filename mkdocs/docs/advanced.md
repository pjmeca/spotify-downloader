---
title: Advanced spotDL and yt-dlp Settings
description: Customize SpotCrate with advanced spotDL options, yt-dlp update policies, and local Docker image builds.
---

# Advanced

## spotDL options

The container builds a spotDL command for each download and appends any extra flags you provide via `OPTIONS`.

### How it is assembled

The command includes:

- `download <url>`
- `--format <FORMAT>`
- `--threads <process thread count>`
- `--client-id` and `--client-secret` (from your environment)
- `--use-official-api`

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

## yt-dlp update policy

The image includes a pinned baseline version of `yt-dlp`, but the container can override it at runtime before each scheduled run.

Set `YT_DLP_UPDATE_POLICY` to one of these values:

- `before-run`: update `yt-dlp` to the latest available version before every cron run.
- `never`: keep the currently installed version and skip updates.
- `<version>`: install that exact `yt-dlp` version before the run. If that version is already installed, the container skips the install.

If the install or update fails, the run continues with the version that is already installed and logs a warning.

## Build the image locally

If you want to build locally (for development or customization), use the Dockerfile in `SpotCrate/`.

### Build command

```bash
docker build -t spotcrate:local ./SpotCrate
```

Then reference it in your compose file:

```yaml
services:
  spotcrate:
    image: spotcrate:local
```

### What is inside

The image includes:

- .NET runtime (from the official ASP.NET base image)
- ffmpeg
- yt-dlp
- spotDL

Pinned baseline versions are defined in `SpotCrate/Dockerfile`. Update that file if you need different versions baked into the image.
