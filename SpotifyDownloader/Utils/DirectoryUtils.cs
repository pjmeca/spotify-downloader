using Microsoft.Extensions.Logging;

namespace SpotifyDownloader.Utils;

public static class DirectoryUtils
{
    public static void MoveAndMerge(string sourceDir, string destinationDir, ILogger? logger = null)
    {
        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        foreach (var sourceFilePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            try
            {
                string relativePath = Path.GetRelativePath(sourceDir, sourceFilePath);
                string destinationFilePath = Path.Combine(destinationDir, relativePath);

                var destinationSubDir = Path.GetDirectoryName(destinationFilePath);
                if (destinationSubDir != null && !Directory.Exists(destinationSubDir))
                {
                    Directory.CreateDirectory(destinationSubDir);
                }

                File.Copy(sourceFilePath, destinationFilePath, true);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "An error occurred while moving the file \"{file}\" from \"{dirFrom}\" to \"{dirTo}\"", sourceFilePath, sourceDir, destinationDir);
            }
        }

        Directory.Delete(sourceDir, true);
    }
}
