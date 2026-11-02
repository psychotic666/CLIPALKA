using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace Clipalka.App.Services;

// Runs the real Windows export path against generated video, without capture hardware.
public static class ReplayExportVerification
{
    public static async Task RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetFullPath(directory));
        var source = await folder.CreateFileAsync("source.mp4", CreationCollisionOption.ReplaceExisting);
        var composition = new MediaComposition();
        composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255, 40, 60, 90), TimeSpan.FromSeconds(2)));
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
        profile.Video.Width = 640;
        profile.Video.Height = 360;
        profile.Video.FrameRate.Numerator = 60;
        profile.Video.FrameRate.Denominator = 1;
        profile.Video.Bitrate = 3_000_000;
        var result = await composition.RenderToFileAsync(source, MediaTrimmingPreference.Precise, profile);
        if (result != TranscodeFailureReason.None) throw new InvalidOperationException("Fixture render failed: " + result);
        var sourceClip = await MediaClip.CreateFromFileAsync(source);
        var sourceVideo = sourceClip.GetVideoEncodingProperties();
        var sourceFps = (double)sourceVideo.FrameRate.Numerator / sourceVideo.FrameRate.Denominator;
        if (Math.Abs(sourceFps - 60) > 1) throw new InvalidOperationException("Fixture has unexpected FPS: " + sourceFps);

        var exporter = new ReplayClipExporter();
        foreach (var (name, sources, seconds) in new[]
        {
            ("single.mp4", new[] { source.Path }, 1d),
            ("joined.mp4", new[] { source.Path, source.Path }, 3d)
        })
        {
            var output = Path.Combine(directory, name);
            await exporter.ExportLastAsync(sources, output, TimeSpan.FromSeconds(seconds));
            var clip = await MediaClip.CreateFromFileAsync(await StorageFile.GetFileFromPathAsync(output));
            var video = clip.GetVideoEncodingProperties();
            if (video.Width != 640 || video.Height != 360 || video.FrameRate.Denominator == 0 ||
                // MediaClip reports an average rational rate; short trimmed clips
                // differ slightly because the endpoints round to whole frames.
                Math.Abs((double)video.FrameRate.Numerator / video.FrameRate.Denominator - sourceFps) > sourceFps * .01 ||
                Math.Abs(clip.OriginalDuration.TotalSeconds - seconds) > .3)
                throw new InvalidOperationException($"Export changed format/duration: {name}, {video.Width}x{video.Height}, {video.FrameRate.Numerator}/{video.FrameRate.Denominator}, {clip.OriginalDuration}");
        }

        var existing = Path.Combine(directory, "single.mp4");
        var original = await File.ReadAllBytesAsync(existing);
        var rejected = false;
        try { await exporter.ExportLastAsync(source.Path, existing, TimeSpan.FromSeconds(1)); }
        catch (Exception) { rejected = true; }
        var afterRejectedExport = await File.ReadAllBytesAsync(existing);
        if (!rejected || !original.SequenceEqual(afterRejectedExport) ||
            Directory.EnumerateFiles(directory, ".render-*.mp4").Any())
            throw new InvalidOperationException("Export overwrite protection or temporary cleanup failed.");
        await File.WriteAllTextAsync(Path.Combine(directory, "result.txt"),
            "PASS: single + joined replay retain 640x360 at 60 FPS; duration validated; existing file protected; temporary outputs cleaned.");
    }
}
