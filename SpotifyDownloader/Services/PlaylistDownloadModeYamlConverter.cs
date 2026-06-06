using SpotifyDownloader.Models;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace SpotifyDownloader.Services;

public class PlaylistDownloadModeYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(PlaylistDownloadMode) || type == typeof(PlaylistDownloadMode?);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var value = parser.Consume<Scalar>().Value;
        return string.Equals(value, "full", StringComparison.OrdinalIgnoreCase)
            ? PlaylistDownloadMode.Full
            : PlaylistDownloadMode.Add;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var mode = value is PlaylistDownloadMode playlistMode && playlistMode == PlaylistDownloadMode.Full
            ? "full"
            : "add";
        emitter.Emit(new Scalar(mode));
    }
}
