$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Build the exact upstream revision plus our small, reviewable PCM fix.
# Never replace files in the global NuGet cache.
$repo = Split-Path $PSScriptRoot -Parent
$revision = '646383234187efd3854e6b1069612b80c3ab9981'
$work = Join-Path $repo ('artifacts/native-build/' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $work 'source'
$output = Join-Path $repo 'artifacts/native'
New-Item -ItemType Directory -Force $work, $output | Out-Null

function Invoke-Checked([string] $Program, [string[]] $Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Program failed with exit code $LASTEXITCODE" }
}

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -version '[17.0,18.0)' -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 Microsoft.VisualStudio.Component.VC.ATL -property installationPath
if (!$vs) { throw 'Install Visual Studio 2022 with Desktop C++, ATL, C++/CLI v143, Windows SDK and .NET Framework 4.8 targeting pack.' }
$msbuild = Join-Path $vs 'MSBuild/Current/Bin/MSBuild.exe'
$devcmd = Join-Path $vs 'Common7/Tools/VsDevCmd.bat'

Invoke-Checked git @('-c', 'core.autocrlf=false', 'clone', '--depth', '1', '--branch', 'v7.0.1', 'https://github.com/sskodje/ScreenRecorderLib.git', $source)
$actual = & git -C $source rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $actual -ne $revision) { throw "Unexpected ScreenRecorderLib revision: $actual" }
$patch = Join-Path $PSScriptRoot 'ScreenRecorderLib-pcm-alignment.patch'
Invoke-Checked git @('-C', $source, 'apply', '--check', $patch)
Invoke-Checked git @('-C', $source, 'apply', $patch)
Copy-Item (Join-Path $PSScriptRoot 'PcmBlockAligner.h') (Join-Path $source 'ScreenRecorderLibNative/PcmBlockAligner.h')

$assemblyInfo = Join-Path $source 'ScreenRecorderLib/AssemblyInfo.cpp'
$text = [IO.File]::ReadAllText($assemblyInfo)
if (!$text.Contains('AssemblyDescriptionAttribute(L"")')) { throw 'Upstream assembly marker changed' }
$text = $text.Replace('AssemblyDescriptionAttribute(L"")', 'AssemblyDescriptionAttribute(L"CLIPALKA PCM FIFO 1")')
$text = $text.Replace('AssemblyVersionAttribute("1.0.*")', 'AssemblyVersionAttribute("7.0.1.1")')
[IO.File]::WriteAllText($assemblyInfo, $text)

# Compile and execute the SAME header included by AudioManager, with assertions.
$testSource = Join-Path $PSScriptRoot 'tests/pcm_alignment_test.cpp'
$testExe = Join-Path $work 'pcm_alignment_test.exe'
$testObj = Join-Path $work 'pcm_alignment_test.obj'
$testCommand = 'call "{0}" -arch=x64 && cl /nologo /std:c++17 /EHsc /W4 /UNDEBUG "{1}" /Fo"{2}" /Fe"{3}" && "{3}"' -f $devcmd, $testSource, $testObj, $testExe
Invoke-Checked cmd.exe @('/d', '/s', '/c', $testCommand)

$project = Join-Path $source 'ScreenRecorderLib/ScreenRecorderLib.vcxproj'
Invoke-Checked $msbuild @($project, '/m', '/nologo', '/verbosity:minimal', '/p:Configuration=Release', '/p:Platform=x64', '/p:PlatformTarget=x64')
$dll = Join-Path $source 'ScreenRecorderLib/bin/x64/Release/ScreenRecorderLib.dll'
if (!(Test-Path $dll)) { throw "Native recorder was not produced: $dll" }
Copy-Item $dll (Join-Path $output 'ScreenRecorderLib.dll') -Force
Copy-Item (Join-Path $PSScriptRoot 'LICENSE-ScreenRecorderLib.txt') $output -Force
@{
    upstream = $revision
    patchSha256 = (Get-FileHash $patch -Algorithm SHA256).Hash
    headerSha256 = (Get-FileHash (Join-Path $PSScriptRoot 'PcmBlockAligner.h') -Algorithm SHA256).Hash
    dllSha256 = (Get-FileHash $dll -Algorithm SHA256).Hash
    marker = 'CLIPALKA PCM FIFO 1'
} | ConvertTo-Json | Set-Content (Join-Path $output 'build-info.json')
Write-Host "Patched recorder and PCM regression tests built successfully: $output"
