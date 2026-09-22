namespace Clipalka.Core.Services;

public static class RecordingPathService
{
    private static readonly HashSet<char> WindowsInvalidFileNameCharacters =
        ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    public static string EnsureOutputDirectory(string? requestedPath)
    {
        var path = string.IsNullOrWhiteSpace(requestedPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "CLIPALKA")
            : Environment.ExpandEnvironmentVariables(requestedPath.Trim());

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public static string CreateVideoPath(string directory, string prefix, DateTimeOffset? timestamp = null)
    {
        var time = timestamp ?? DateTimeOffset.Now;
        var safePrefix = string.Concat(prefix.Where(character =>
            !Path.GetInvalidFileNameChars().Contains(character) &&
            !WindowsInvalidFileNameCharacters.Contains(character)));
        return Path.Combine(directory, $"{safePrefix}_{time:yyyy-MM-dd_HH-mm-ss}.mp4");
    }
}
