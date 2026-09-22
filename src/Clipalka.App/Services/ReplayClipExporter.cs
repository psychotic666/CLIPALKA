using Windows.Media.Editing;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace Clipalka.App.Services;

public sealed class ReplayClipExporter
{
    public async Task ExportLastAsync(string sourcePath, string destinationPath, TimeSpan duration)
    {
        var source = await StorageFile.GetFileFromPathAsync(sourcePath);
        var destinationFolder = await StorageFolder.GetFolderFromPathAsync(
            Path.GetDirectoryName(destinationPath)!);
        var destination = await destinationFolder.CreateFileAsync(
            Path.GetFileName(destinationPath), CreationCollisionOption.GenerateUniqueName);

        var clip = await MediaClip.CreateFromFileAsync(source);
        if (clip.OriginalDuration > duration)
        {
            clip.TrimTimeFromStart = clip.OriginalDuration - duration;
        }

        var composition = new MediaComposition();
        composition.Clips.Add(clip);
        var result = await composition.RenderToFileAsync(destination, MediaTrimmingPreference.Precise);
        if (result != TranscodeFailureReason.None)
        {
            throw new InvalidOperationException($"Windows не смог обрезать replay: {result}.");
        }
    }
}
