---
title: Home
description: SpotCrate tracks Spotify artists and playlists from Docker and keeps your tagged music library up to date automatically.
hide:
  - navigation
  - toc
---

<section class="spotcrate-hero">
  <div class="spotcrate-hero__content">
    <div class="spotcrate-hero__brand">
      <img src="/assets/images/isotype.webp" alt="" aria-hidden="true" />
      <h1 class="spotcrate-hero__wordmark"><span>Spot</span><span>Crate</span></h1>
    </div>
    <p>Track Spotify artists and playlists from Docker and keep your tagged music library up to date automatically.</p>
    <div class="spotcrate-hero__actions">
      <a href="getting-started/">Get started</a>
      <a href="https://github.com/pjmeca/spotcrate" class="spotcrate-hero__icon-link" aria-label="Open SpotCrate on GitHub">
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 .5a12 12 0 0 0-3.79 23.39c.6.11.82-.26.82-.58v-2.03c-3.34.73-4.04-1.42-4.04-1.42-.55-1.39-1.34-1.76-1.34-1.76-1.09-.75.08-.74.08-.74 1.21.09 1.85 1.24 1.85 1.24 1.07 1.84 2.81 1.31 3.5 1 .11-.78.42-1.31.76-1.61-2.67-.3-5.47-1.33-5.47-5.93 0-1.31.47-2.38 1.24-3.22-.12-.3-.54-1.52.12-3.18 0 0 1.01-.32 3.3 1.23a11.4 11.4 0 0 1 6 0c2.29-1.55 3.3-1.23 3.3-1.23.66 1.66.24 2.88.12 3.18.77.84 1.24 1.91 1.24 3.22 0 4.61-2.81 5.63-5.49 5.93.43.37.81 1.1.81 2.22v3.29c0 .32.22.7.83.58A12 12 0 0 0 12 .5Z" /></svg>
        GitHub
      </a>
      <a href="https://hub.docker.com/r/pjmeca/spotcrate" class="spotcrate-hero__icon-link" aria-label="Open SpotCrate on Docker Hub">
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M13.12 7.33h2.18v2.22h1.1c.5 0 1.02-.09 1.49-.25.23-.08.5-.19.72-.34-.29-.39-.44-.87-.48-1.35-.06-.65.07-1.5.51-2l.22-.25.26.2c.66.51 1.21 1.23 1.31 2.07.8-.23 1.74-.18 2.44.23l.29.17-.15.3c-.6 1.17-1.86 1.54-3.09 1.48-1.84 4.58-5.85 6.75-10.7 6.75-2.51 0-4.81-.94-6.12-3.15l-.02-.04-.2-.41c-.47-1.04-.62-2.28-.5-3.41l.03-.31h1.77V7.33h2.18V5.16h4.36v2.17h2.4Zm-6.15 0h2.03V5.86H6.97v1.47Zm-2.18.7v1.52h2.03V8.03H4.79Zm2.73 0v1.52h2.03V8.03H7.52Zm2.73 0v1.52h2.03V8.03h-2.03Zm2.73 0v1.52h2.03V8.03h-2.03ZM5.2 10.25H3.1c-.03.85.12 1.75.47 2.53l.16.33c1.13 1.88 3.1 2.75 5.49 2.75 4.53 0 8.15-2.04 9.73-6.35l.08-.22.23.03c.97.13 1.97-.04 2.56-.74-.65-.2-1.46-.13-2.05.23l-.56.34.04-.65c.05-.72-.31-1.37-.82-1.84-.17.32-.22.75-.18 1.11.05.51.27.95.66 1.24l.33.25-.34.24c-.27.19-.56.34-.87.46-.6.24-1.27.36-1.93.36H5.2Z" /></svg>
        Docker Hub
      </a>
    </div>
  </div>
</section>

SpotCrate watches the Spotify artists and playlists you choose, downloads new music on a schedule, and saves everything into clean artist and playlist folders. Start with the [Docker Compose setup](getting-started.md), then adjust your [tracking file](configuration.md#trackingyaml) and [schedule](operations.md#scheduling) when needed.

<p class="spotcrate-premium-note">An active <strong>Spotify Premium subscription</strong> is required to use SpotCrate.</p>

SpotCrate is a personal, self-hosted project and is not affiliated with Spotify, YouTube, Google, spotDL, or yt-dlp. See the [project disclaimer](about.md#disclaimer).

## Why use SpotCrate

<div class="spotcrate-feature-grid">
  <div class="spotcrate-feature-card"><strong>Automatic updates</strong><span>Run on a <a href="operations#scheduling">schedule</a> and keep your library updated without babysitting downloads.</span></div>
  <div class="spotcrate-feature-card"><strong>Simple tracking</strong><span>Track your chosen artists and playlists with <a href="configuration#trackingyaml">a simple file</a>.</span></div>
  <div class="spotcrate-feature-card"><strong>Flexible formats</strong><span>Choose <code>opus</code>, <code>mp3</code>, or another <a href="advanced#spotdl-options">spotDL-compatible format</a>.</span></div>
  <div class="spotcrate-feature-card"><strong>Clean folders</strong><span>Save downloads into predictable artist and playlist folders under your music directory.</span></div>
  <div class="spotcrate-feature-card spotcrate-feature-card--wide"><strong>Optional web UI</strong><span>Manage, sort, and reorder tracked items from the <a href="configuration#web-ui">browser editor</a>.</span></div>
</div>

## How it works

1. Define artists and playlists in `tracking.yaml`, either manually or through the optional [web UI](configuration.md#web-ui).
2. The container checks what you already have.
3. New content is downloaded and tagged.
4. Your library stays tidy and up to date.

<p align="center">
  <img src="/assets/images/illustration.webp" alt="SpotCrate workflow illustration"/>
</p>

## Quick start

If you already have [Docker](https://docs.docker.com/engine/install/) installed, follow [Getting Started](getting-started.md) to launch SpotCrate in minutes.
