# About

## The .NET rewrite

In September 2024, version `2.0.0` was released as a full rewrite in .NET. The previous implementation relied more heavily on spotDL behavior, but a long series of `429` rate limit errors from the Spotify API made it difficult to keep reliable downloads.

After trying the recommended mitigations described in the spotDL issue tracker, the decision was made to build a dedicated scheduler and tracking layer in .NET, while still using spotDL for the actual download and tagging.

The result is a container that focuses on:

- predictable scheduling
- flexible tracking rules
- better control over the download workflow

The choice of .NET was mostly pragmatic: it matches the maintainer's day-to-day tooling and makes iteration faster.

## Credits

Special thanks to the projects that make this possible:

- [spotDL](https://github.com/spotDL/spotify-downloader) for the core download engine
- [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET) for the Spotify API client
- [TagLib#](https://github.com/mono/taglib-sharp) for audio metadata access
- [YamlDotNet](https://github.com/aaubry/YamlDotNet) for YAML parsing
- [EasyCronJob](https://github.com/furkandeveloper/EasyCronJob) for cron scheduling in .NET
- Picture of a hand holding headsets from the banner by [Sound On](https://www.pexels.com/es-es/foto/foto-de-persona-sosteniendo-auriculares-blancos-3761020/)
