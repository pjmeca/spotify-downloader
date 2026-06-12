---
hide:
  - navigation
  - toc
---

<p align="center" class="banner">
  <img src="assets/images/banner-dark.webp#only-dark" alt="banner"/>
</p>

<p align="center" class="banner">
  <img src="assets/images/banner-light.webp#only-light" alt="banner"/>
</p>

# Home

Spotify Downloader is a Docker image that tracks artists and playlists on Spotify and automatically downloads new music to your library using [spotDL](https://github.com/spotDL/spotify-downloader) and [yt-dlp](https://github.com/yt-dlp/yt-dlp). Tracks are sourced from YouTube and tagged with Spotify metadata, so everything lands neatly organized and ready to play.

???+ warning "Recent Spotify Developer policy changes"
    Spotify has recently changed its **Spotify for Developers** policy and the API now requires the **app owner to have an active Spotify Premium subscription**.

    This affects how this project works and may cause it to stop functioning if the app owner does not have Premium.

    See the full discussion in **[#51](https://github.com/pjmeca/spotify-downloader/issues/51)**.

## Why you will like it

- 🚀 **Set it and forget it**: run on a schedule and keep your library updated.
- 🧠 **Smart tracking**: skip rarely updated artists, or fully sync playlists when you want.
- 🎚️ **Flexible formats**: use `opus`, `mp3`, or any spotDL-compatible format.
- 🗂️ **Clean structure**: artists and playlists are organized automatically.
- 🖥️ **Optional web UI**: manage, sort, and reorder `tracking.yaml` from a responsive browser interface.

## How it works

1. Define artists and playlists in `tracking.yaml`, either manually or through the optional web UI.
2. The container checks what you already have.
3. New content is downloaded and tagged.
4. Your library stays tidy and up to date.

<p align="center">
  <img src="assets/images/illustration.webp" alt="banner"/>
</p>

## Quick start

If you already have [Docker](https://docs.docker.com/engine/install/) installed, head to [Getting Started](getting-started.md) to launch in minutes.
