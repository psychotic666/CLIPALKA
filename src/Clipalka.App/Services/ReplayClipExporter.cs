using Windows.Media.Editing;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace Clipalka.App.Services;

public sealed class ReplayClipExporter
{
    public async Task ExportLastAsync(string sourcePath, string destinationPath, TimeSpan duration)
    {
        await ExportLastAsync([sourcePath], destinationPath, duration);
    }

    public async Task ExportLastAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationPath,
        TimeSpan duration)
    {
        var sourceClips = new List<MediaClip>();
        foreach (var sourcePath in sourcePaths.Where(File.Exists))
        {
            var source = await StorageFile.GetFileFromPathAsync(sourcePath);
            sourceClips.Add(await MediaClip.CreateFromFileAsync(source));
        }

        if (sourceClips.Count == 0)
        {
            throw new InvalidOperationException("Replay-буфер пока не содержит видео.");
        }

        var destinationFolder = await StorageFolder.GetFolderFromPathAsync(
            Path.GetDirectoryName(destinationPath)!);
        var destination = await destinationFolder.CreateFileAsync(
            Path.GetFileName(destinationPath), CreationCollisionOption.ReplaceExisting);

        var trimFromStart = sourceClips.Aggregate(TimeSpan.Zero, (total, clip) => total + clip.OriginalDuration) - duration;
        var composition = new MediaComposition();
        foreach (var clip in sourceClips)
        {
            if (trimFromStart >= clip.OriginalDuration)
            {
                trimFromStart -= clip.OriginalDuration;
                continue;
            }

            if (trimFromStart > TimeSpan.Zero)
            {
                clip.TrimTimeFromStart = trimFromStart;
                trimFromStart = TimeSpan.Zero;
            }
            composition.Clips.Add(clip);
        }

        var result = await composition.RenderToFileAsync(destination, MediaTrimmingPreference.Fast);
        if (result != TranscodeFailureReason.None)
        {
            throw new InvalidOperationException($"Windows не смог обрезать replay: {result}.");
        }
    }
}
