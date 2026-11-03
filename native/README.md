# Native PCM alignment

CLIPALKA builds ScreenRecorderLib 7.0.1 from commit
`646383234187efd3854e6b1069612b80c3ab9981` with the adjacent patch.
Upstream: https://github.com/sskodje/ScreenRecorderLib (MIT).

The original mixer sizes every source preview and the mixed output to the largest
resampled block. A shorter live microphone block acquires a zero tail.
In the supplied diagnostic WAV, 63 of 556 voiced packet boundaries in two sessions
contained an exact one-frame stereo zero tail before AAC encoding. This supports
a packet-boundary defect; it does not prove that every artifact in a newer AAC
clip has the same cause, or that microphone gain itself causes the mismatch.

`PcmBlockAligner.h` keeps each source's unused PCM frames across callbacks and emits
only the common ready length for active sources. A backend-confirmed silent source
can be padded. Queues are cleared on stop, pause and source removal. A stalled
source cannot accumulate more than 96,000 frames (two seconds at the configured
48 kHz); overflow fails capture instead of silently discarding or corrupting audio.
No audio effects, gain amplification, de-click filters or codec settings are changed.

## Windows build

Install Visual Studio 2022 Desktop C++, C++/CLI v143, a Windows SDK and the
.NET Framework 4.8 targeting pack, plus Git and .NET 8 SDK. From the repository root:

```powershell
./native/Build-Recorder.ps1
dotnet restore CLIPALKA.sln
dotnet test CLIPALKA.sln -c Release
dotnet publish src/Clipalka.App/Clipalka.App.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o publish/win-x64
dotnet publish/win-x64/CLIPALKA.dll --verify-native-audio native-audio-verification
```

The script verifies the pinned upstream revision, applies the patch, executes
the PCM regression test with MSVC and writes a DLL plus source/patch/DLL hashes
under `artifacts/native`. It never modifies the NuGet cache. Windows app builds
require this DLL; the project replaces NuGet's injected reference before resolving
assemblies. CI compares built and published hashes and executes real native audio
enumeration under .NET 8. The assembly description must be `CLIPALKA PCM FIFO 1`.
Non-Windows managed cross-compilation can still use stock NuGet and does **not**
validate or produce the patched capture engine.

Portable regression test on macOS/Linux:

```sh
clang++ -std=c++17 -Wall -Wextra -pedantic -fsanitize=undefined native/tests/pcm_alignment_test.cpp -o /tmp/clipalka-pcm-test
/tmp/clipalka-pcm-test
```

The test reproduces the old zero tail and verifies exact count/order across 2,000
unequal stereo blocks at each gain (100%, 68%, 50%, 10%), inactive silence,
temporary underrun, disconnect, reset, invalid frames and bounded queues.
Hardware acceptance still requires a new Sonar Gaming + Sonar Microphone replay
at both 100% and reduced microphone volume. CI has no Sonar devices.
