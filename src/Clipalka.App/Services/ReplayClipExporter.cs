using Windows.Media.Editing;
using Windows.Media.MediaProperties;
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

        // Set the profile explicitly: export must not silently choose a different
        // resolution or frame rate than the recorder's original segments.
        var video = composition.Clips.Last().GetVideoEncodingProperties();
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
        profile.Video.Width = video.Width;
        profile.Video.Height = video.Height;
        profile.Video.FrameRate.Numerator = video.FrameRate.Numerator;
        profile.Video.FrameRate.Denominator = video.FrameRate.Denominator;
        profile.Video.Bitrate = video.Bitrate > 0 ? video.Bitrate : 60_000_000;
        profile.Audio.SampleRate = 48000;
        profile.Audio.ChannelCount = 2;
        profile.Audio.Bitrate = 192000;

        var destinationFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(destinationPath)!);
        var temporaryName = $".render-{Guid.NewGuid():N}.mp4";
        var destination = await destinationFolder.CreateFileAsync(temporaryName, CreationCollisionOption.FailIfExists);
        try
        {
            var result = await composition.RenderToFileAsync(destination, MediaTrimmingPreference.Fast, profile);
            if (result != TranscodeFailureReason.None)
                throw new InvalidOperationException($"Windows не смог обрезать replay: {result}.");
            await destination.RenameAsync(Path.GetFileName(destinationPath), NameCollisionOption.FailIfExists);
        }
        catch
        {
            try { await destination.DeleteAsync(StorageDeleteOption.PermanentDelete); }
            catch (Exception) { /* Preserve the export failure if cleanup also fails. */ }
            throw;
        }
    }
}
