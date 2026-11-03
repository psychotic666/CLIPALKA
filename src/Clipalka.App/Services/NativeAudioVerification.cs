using System.Reflection;
using ScreenRecorderLib;

namespace Clipalka.App.Services;

internal static class NativeAudioVerification
{
    public static string Run()
    {
        var assembly = typeof(Recorder).Assembly;
        var marker = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
        if (marker != "CLIPALKA PCM FIFO 1")
            throw new InvalidOperationException($"Stock or unexpected recorder loaded: {assembly.Location}, {marker}");

        // Execute native C++/CLI code under .NET 8, rather than only inspecting metadata.
        // A CI machine may legitimately have zero audio devices.
        var inputs = Recorder.GetSystemAudioCaptureDevices();
        var outputs = Recorder.GetSystemAudioLoopbackDevices();
        return $"PASS: {marker}; native audio enumeration executed; inputs={inputs.Count}, outputs={outputs.Count}; assembly={assembly.Location}";
    }
}
