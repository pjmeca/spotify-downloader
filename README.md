# pjmeca/spotify-downloader

[![GitHub Repo stars](https://img.shields.io/github/stars/pjmeca/spotify-downloader?style=flat&logo=github&label=Star%20this%20repo!)](https://github.com/pjmeca/spotify-downloader)
[![Docker Image Version (tag)](https://img.shields.io/docker/v/pjmeca/spotify-downloader/latest?logo=docker)](https://hub.docker.com/r/pjmeca/spotify-downloader)

This Docker image periodically tracks and downloads new music for your library using [spotDL](https://github.com/spotDL/spotify-downloader). New tracks are downloaded from YouTube and Spotify's metadata is embedded. You can chose from various formats (the default is `opus`) and add custom options to the spotDL execution command. For more information, [read the docs](https://spotdl.readthedocs.io).

You can find the Dockerfile and all the resources used to create this image in [my GitHub repository](https://github.com/pjmeca/spotify-downloader). If you find this useful, please leave a ⭐. Feel free to request new features *or make a pull request if you're up for it!* 💪

## Usage

The following `docker-compose` creates a container that downloads new music everyday at 00:00 AM.

```yml docker-compose.yml
name: spotify-downloader

services:
  spotify-downloader:
    image: pjmeca/spotify-downloader:latest
    container_name: spotify-downloader
    restart: unless-stopped
    volumes:
      - /your/main/music/path:/music # (Required) Change this
      - /path/to/tracking.yaml:/app/tracking.yaml:ro # (Required) Change this
      - /logs:/app/logs # (Optional)
    environment:
      CRON_SCHEDULE: "0 0 * * *" # (Required) Customize your cron if needed
      CLIENT__ID: "y0ur5p071fycl13n71d" # (Required) Change this
      CLIENT__SECRET: "y0ur5p071fycl13n753cr37" # (Required) Change this
      TZ: Europe/Madrid # (Recommended) Your timezone
      FORMAT: "opus" # (Optional) Music format. Must be compatible with spotDL. Defaults to "opus".
      OPTIONS: "" # (Optional) Additional spotDL options. Don't add here your Spotify credentials.
```

Then run:

```
docker compose -f ./docker-compose.yml up -d
```

## About `tracking.yaml`

Each time the script inside the container runs, it reads the `tracking.yaml` file **(you must supply this file as a read-only volume)** and downloads all its contents. You don't need to stop or redeploy your container each time the file gets updated; changes will be read automatically on the next run.

Below is an example of `tracking.yaml`. The `name` field is used as a folder name, which will be created if it does not exist. If you want to download multiple URLs to the same folder, create multiple entries with the same name.

Optionally, you can specify if you wish to `refresh` each entry (defaults to `True`) in case it already contains files. It is advised to set this field for artists who do not publish new tracks frequently, as it will drastically decrease Spotify's API calls, thus reducing the risk of receiving too many 429 HTTP codes.

```yaml
artists:
  - name: Dua Lipa
    url: https://open.spotify.com/intl-es/artist/6M2wZ9GZgrQXHCFfjv46we
  - name: The Beatles
    url: https://open.spotify.com/intl-es/artist/3WrFJ7ztbogyGnTHbHJFl2
    refresh: false
  - name: Olivia Rodrigo
    url: https://open.spotify.com/intl-es/artist/1McMsnEElThX1knmY4oliG
    refresh: true

playlists:
  - name: Los 90 España
    url: https://open.spotify.com/playlist/37i9dQZF1DWXm9R2iowygp
```

### Result

```bash
user@host:/music$ tree -d
.
├── Dua Lipa
├── Los 90 España
├── Olivia Rodrigo
└── The Beatles
```


## Changelog

- 2.1.1: Prevent OOM issues & Avoid `null` albums to be a key of the dictionary
- 2.1.0: Smart Deletion & Organize files into subfolders
- 2.0.0: .Net Release
- 1.1.2: Upgrade `spotdl` to `v4.2.8`
- 1.1.1: Fix: A debug directory was mistakenly introduced in the build of the release image
- 1.1.0: Added `refresh` field to `tracking.yaml`
- 1.0.0: Added playlists; first stable release
- 0.0.4: Fixed download directories (again)
- 0.0.3: Displayed program start & end time
- 0.0.2: Fixed download directories
- 0.0.1: Initial release

> [!NOTE]
> ### About the .Net release
> In September 2024, version `2.0.0` of `spotify-downloader` was released. This major version included a rewrite of the original code in Python. The reason behind this decision was to address a continuous series of `429` errors from the Spotify API unhandled by `spotDL`. I followed all the recomendations suggested in their [issue](https://github.com/spotDL/spotify-downloader/issues/2142), but it still didn't work. So, I decided to take a different approach, giving myself more flexibility to achieve what I really need.
> 
> The reason why this change was made in .NET instead of sticking with Python is just that I am much more fluent in the former.

## Special Thanks To

- The [spotDL project](https://github.com/spotDL/spotify-downloader) for providing the core functionality of this image.
- The [cron project](https://github.com/lnquy/cron) for offering an easy way to display cron expressions in a human-friendly way.