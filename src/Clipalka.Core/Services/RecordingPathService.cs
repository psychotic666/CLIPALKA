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

    public static string CreateCapturePath(
        string directory,
        string? applicationName,
        bool isReplay,
        DateTimeOffset? timestamp = null)
    {
        var time = timestamp ?? DateTimeOffset.Now;
        var requestedName = string.IsNullOrWhiteSpace(applicationName) ? "CLIPALKA" : applicationName.Trim();
        var safeName = string.Concat(requestedName.Where(character =>
            !Path.GetInvalidFileNameChars().Contains(character) &&
            !WindowsInvalidFileNameCharacters.Contains(character))).Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "CLIPALKA";
        }

        var replaySuffix = isReplay ? " Replay" : string.Empty;
        return Path.Combine(directory, $"{safeName}{replaySuffix} - {time:yyyy-MM-dd HH-mm-ss}.mp4");
    }
}
