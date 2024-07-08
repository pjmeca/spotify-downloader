# pjmeca/spotify-downloader

This Docker image periodically tracks and downloads new music for your library using [spotDL](https://github.com/spotDL/spotify-downloader). New tracks are downloaded from YouTube and Spotify's metadata is embedded. You can chose from various formats (the default is "opus") and add custom options to the spotDL execution command. For more information, [read the docs](https://spotdl.readthedocs.io).

I created this image to keep my music library updated automatically as new tracks are released. spotDL is a fantastic tool; all credit go to its team. I just repackaged it with cron and a script that reads a YAML file.

You can find the Dockerfile and all the resources used to create this image in [my GitHub repository](https://github.com/pjmeca/spotify-downloader). If you find this useful, please leave a ⭐. Feel free to request new features *or make a pull request if you're up for it!* 💪

## Usage

The following example creates a container that downloads new music everyday at 00:00 AM.

### Using docker run:

```sh
docker run --name spotify-downloader -v /your/main/music/path:/music -v /path/to/tracking.yaml:/app/tracking.yaml:ro -v /etc/localtime:/etc/localtime:ro -e CRON_SCHEDULE="0 0 * * *" --restart unless-stopped pjmeca/spotify-downloader:latest
```

### Using docker-compose:

```yml docker-compose.yml
name: spotify-downloader

services:
  spotify-downloader:
    image: pjmeca/spotify-downloader:latest
    container_name: spotify-downloader
    volumes:
      - /your/main/music/path:/music # Change this
      - /path/to/tracking.yaml:/app/tracking.yaml:ro # Change this
      - /etc/localtime:/etc/localtime:ro
    environment:
      CRON_SCHEDULE: "0 0 * * *" # Customize your cron if needed
      #FORMAT: "opus" # Music format. Must be compatible with spotDL. Defaults to "opus".
      #OPTIONS: "--client-id <YOUR_ID> --client-secret <YOUR_SECRET> # Additional spotDL options. I like to add here my Spotify credentials.
    restart: unless-stopped
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
    url: https://open.spotify.com/intl-es/artist/6M2wZ9GZgrQXHCFfjv46we?si=p0oIec8oSkWbb0FSJ1CHVw
  - name: The Beatles
    url: https://open.spotify.com/intl-es/artist/3WrFJ7ztbogyGnTHbHJFl2?si=s1ZRiu9rT6WbL5WPtW3rDA
    refresh: false
  - name: Olivia Rodrigo
    url: https://open.spotify.com/intl-es/artist/1McMsnEElThX1knmY4oliG?si=1AJfFUAgTdquWXOI7643xw
    refresh: true

playlists:
  - name: Los 90 España
    url: https://open.spotify.com/playlist/37i9dQZF1DWXm9R2iowygp?si=7a8a840ee1b14436
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

- 1.1.0: Added `refresh` field to `tracking.yaml`
- 1.0.0: Added playlists; first stable release
- 0.0.4: Fixed download directories (again)
- 0.0.3: Displayed program start & end time
- 0.0.2: Fixed download directories
- 0.0.1: Initial release

## Special Thanks To

- The [spotDL project](https://github.com/spotDL/spotify-downloader) for providing the core functionality of this image.
- The [cron project](https://github.com/lnquy/cron) for offering an easy way to display cron expressions in a human-friendly way.