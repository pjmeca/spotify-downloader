using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using static SpotifyAPI.Web.ArtistsAlbumsRequest;

namespace SpotCrate.Services;

/// <summary>
/// This is a wrapper for the SpotifyClient that adds control for TooManyRequests exceptions.
/// </summary>
public class SpotifyClientWrapper(ILogger<SpotifyClientWrapper> _logger, SpotifyClient _spotifyClient)
{
    private async Task<T> TooManyRequestsWrapper<T>(Func<Task<T>> func)
    {
        while (true)
        {
            try
            {
                return await func();
            }
            catch (APITooManyRequestsException ex)
            {
#pragma warning disable S6667 // Logging in a catch clause should pass the caught exception as a parameter.
                _logger.LogError("Received a Too Many Requests error. Retrying After: {retryAfter}", ex.RetryAfter);
#pragma warning restore S6667 // Logging in a catch clause should pass the caught exception as a parameter.
                Thread.Sleep(ex.RetryAfter);
            }
        }
    }

    /// <inheritdoc cref="IArtistsClient.GetAlbums(string, CancellationToken)"/>
    public async Task<IList<SimpleAlbum>> GetAllAlbums(string artistId)
    {
        return await TooManyRequestsWrapper(async () =>
        {
            var firstAlbum = await _spotifyClient.Artists.GetAlbums(artistId, new ArtistsAlbumsRequest()
            {
                IncludeGroupsParam = IncludeGroups.Album | IncludeGroups.Single | IncludeGroups.AppearsOn,
                Limit = 50
            });
            var albums = await _spotifyClient.PaginateAll(firstAlbum);

            return albums;
        });
    }

    /// <inheritdoc cref="IAlbumsClient.GetTracks(string, CancellationToken)"/>
    public async Task<IList<SimpleTrack>> GetAllAlbumTracks(string albumId)
    {
        return await TooManyRequestsWrapper(async () =>
        {
            var firstTrack = await _spotifyClient.Albums.GetTracks(albumId);
            var albumTracks = await _spotifyClient.PaginateAll(firstTrack);

            return albumTracks;
        });
    }

    /// <inheritdoc cref="IPlaylistsClient.GetItems(string,System.Threading.CancellationToken)"/>
    public Task<IList<PlaylistTrack<IPlayableItem>>> GetAllPlaylistTracks(string playlistId)
        => TooManyRequestsWrapper(async () =>
        {
            var firstTrack = await _spotifyClient.Playlists.GetItems(playlistId);
            var playlistTracks = await _spotifyClient.PaginateAll(firstTrack);
            
            return playlistTracks;
        });
}
