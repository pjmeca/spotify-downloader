# Operations

This page covers scheduling, logs, cache, and troubleshooting.

## Scheduling

The job runs on a cron schedule controlled by `CRON_SCHEDULE`.

### Cron format

The scheduler uses the standard 5-field cron format ([help](https://crontab.guru)):

```
minute hour day-of-month month day-of-week
```

Examples:

- `0 0 * * *` - every day at 00:00
- `0 */6 * * *` - every 6 hours
- `30 3 * * 1` - every Monday at 03:30

### Time zone

The cron schedule runs in the container local time zone. Set `TZ` to an IANA time zone like `Europe/Madrid` to match your region.

### Overlapping runs

The scheduler runs a single job at a time. If a run is still executing when the next trigger arrives, that trigger is cancelled.

### Run immediately (testing)

If you want a run to happen on startup, set `DOTNET_ENVIRONMENT=Development`. This is useful for testing, but avoid it in production if you only want scheduled runs.

### Rate limiting

Spotify can return `429` errors if you hit the API too frequently. Use a reasonable schedule and consider `refresh: false` for artists that rarely change.

## Logs and cache

### Logs

By default, logs are written to `/app/logs`:

- `debug-*.log` (retained: 5 files)
- `info-*.log` (retained: 31 files)
- `error-*.log` (retained: 31 files)

Mount `/app/logs` to keep log history between restarts.

### Cache

The cache lives at `/app/cache/application.db` and stores local artist and album metadata plus the current app version. Persisting this directory helps avoid repeated work and reduces API calls.

You can delete the cache if needed. The app will rebuild it over time, but you may see extra API calls or reprocessing on the next run.

## Troubleshooting


??? question "The container exits immediately"
    - Check the logs for `Missing required configuration`.
    - Ensure `CRON_SCHEDULE`, `CLIENT__ID`, and `CLIENT__SECRET` are set.

??? question "I have `tracking.yaml` errors"
    - If the file is missing or invalid YAML, the run will fail.
    - Make sure the file is mounted at `/app/tracking.yaml` and is readable.

??? question "No new downloads"
    - You may already be up to date.
    - `refresh: false` skips items that already exist locally.
    - <u>Private playlists are not accessible with client credentials.</u>
    - Please, check the [logs](#logs-and-cache) for more information

???+ question "YT-DLP errors"
    YouTube tends to be the source of many issues due to frequent changes aimed at preventing scraping. If you encounter a YT-DLP error:

    - Make sure you are using the latest image. You can check it out [here](https://github.com/pjmeca/spotify-downloader/releases/latest).
    - YouTube may be blocking your IP. Changing your IP (or using a different network) may resolve the issue.
    - It could be caused by a recent change on YouTube. Please check our [repo](https://github.com/pjmeca/spotify-downloader/issues) and open an issue so we can look into it as soon as possible.

??? question "spotDL timeouts"
    - Each spotDL process is limited to about 10 minutes.
    - Large albums or slow connections can trigger a timeout.
    - Try running more frequently so each run is smaller.

??? question "Rate limiting (429)"
    - Reduce how often the job runs.
    - Use `refresh: false` for rarely updated artists or directly comment out some items from the `tracking.yaml`.

???+ bug "Still stuck?"
    If you hit an error that is not covered here, open an issue on GitHub:
    
    <https://github.com/pjmeca/spotify-downloader/issues>