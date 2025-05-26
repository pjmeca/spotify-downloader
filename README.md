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
      - /path/to/cache:/app/cache # (Recommended) Store the SQLite cache somewhere
      - /path/to/logs:/app/logs # (Optional)
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

Optionally, you can specify if you wish to `refresh` each entry (defaults to `true`) in case it already contains files. It is advised to set this field for artists who do not publish new tracks frequently, as it will drastically decrease Spotify's API calls, thus reducing the risk of receiving too many 429 HTTP codes.

Playlists support an optional `mode` field. It accepts two values:
- `add` (default): new tracks found in the remote playlist are added to the local folder, but no files are removed.
- `full`: the local folder is synchronized with the remote playlist. Any local tracks no longer present in the remote playlist will be removed.

```yaml
artists:
  - name: Dua Lipa
    url: https://open.spotify.com/intl-es/artist/6M2wZ9GZgrQXHCFfjv46we
  - name: Olivia Rodrigo
    url: https://open.spotify.com/intl-es/artist/1McMsnEElThX1knmY4oliG
    refresh: true # optional, default is 'true'
  - name: The Beatles
    url: https://open.spotify.com/intl-es/artist/3WrFJ7ztbogyGnTHbHJFl2
    refresh: false

playlists:
  - name: Los 90 España
    url: https://open.spotify.com/playlist/37i9dQZF1DWXm9R2iowygp
    mode: full # optional, default is 'add'
```

### Result

```bash
user@host:/music$ tree -d -L 2
.
├── Artists
│   ├── Dua Lipa
│   ├── Olivia Rodrigo
│   └── The Beatles
├── cache
└── Playlists
    └── Los 90 España
```

## About the .Net release
In September 2024, version `2.0.0` of `spotify-downloader` was released. This major version included a rewrite of the original code in Python. The reason behind this decision was to address a continuous series of `429` errors from the Spotify API unhandled by `spotDL`. I followed all the recomendations suggested in their [issue](https://github.com/spotDL/spotify-downloader/issues/2142), but it still didn't work. So, I decided to take a different approach, giving myself more flexibility to achieve what I really need.

The reason why this change was made in .NET instead of sticking with Python is just that I am much more fluent in the former.

## Special Thanks To

- The [spotDL project](https://github.com/spotDL/spotify-downloader), which powers the core music downloading functionality for seamless integration.
- The [SpotifyAPI-NET project](https://github.com/JohnnyCrazy/SpotifyAPI-NET) for providing an intuitive interface to communicate with the Spotify API.
- [TagLib#](https://github.com/mono/taglib-sharp) for providing an easy way to read the metadata from the tracks stored in the file system.
- [YamlDotNet](https://github.com/aaubry/YamlDotNet) for providing a fast and easy way to read Yaml files within .Net.
- The [EasyCronJob project](https://github.com/furkandeveloper/EasyCronJob) for providing an easy way to implement a cron job in .Net.