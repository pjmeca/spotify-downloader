namespace SpotifyDownloader.Utils;

public static class DirectoryUtils
{
    public static void MoveAndMerge(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        foreach (var sourceFilePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
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

        Directory.Delete(sourceDir, true);
    }
}
