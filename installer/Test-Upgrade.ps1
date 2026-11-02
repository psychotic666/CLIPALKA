param([Parameter(Mandatory)][string]$Compiler)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('CLIPALKA-install-test-' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $root 'payload'
$installed = Join-Path $root 'installed'
$setups = Join-Path $root 'setups'
$evidence = Join-Path $PWD 'artifacts/installer-verification'
New-Item -ItemType Directory -Path $payload, $setups, $evidence -Force | Out-Null
$script = Join-Path $PSScriptRoot 'CLIPALKA.iss'
$testId = '{{817BA2F7-46D5-47B4-850F-368B4D457B1F}'
$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{817BA2F7-46D5-47B4-850F-368B4D457B1F}_is1'

function Invoke-CheckedProcess([string]$File, [string[]]$Arguments) {
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Process failed ($($process.ExitCode)): $File" }
}

function Compile-TestInstaller([string]$Version, [string]$Name) {
    & $Compiler "/DMyAppVersion=$Version" "/DMyAppId=$testId" "/DMyAppSourceDir=$payload" '/DTestInstaller' "/O$setups" "/F$Name" $script | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed: $Version" }
    return Join-Path $setups "$Name.exe"
}

# A harmless real executable is sufficient; test installs never execute it.
Copy-Item -LiteralPath (Join-Path $env:WINDIR 'System32/cmd.exe') -Destination (Join-Path $payload 'CLIPALKA.exe')
Set-Content -LiteralPath (Join-Path $payload 'obsolete.dll') -Value 'old package dependency'
Set-Content -LiteralPath (Join-Path $payload 'modified.dll') -Value 'old resource'
Set-Content -LiteralPath (Join-Path $payload 'version.txt') -Value 'old'
& (Join-Path $PSScriptRoot 'New-InstallManifest.ps1') -SourceDirectory $payload
$oldSetup = Compile-TestInstaller '0.9.2' 'old-setup'
Invoke-CheckedProcess $oldSetup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=`"$installed`"", "/LOG=`"$evidence/old-install.log`"")

New-Item -ItemType Directory -Path (Join-Path $installed 'Clips') | Out-Null
$clip = Join-Path $installed 'Clips/keep.mp4'
$settings = Join-Path $installed 'settings.json'
$unknown = Join-Path $installed 'unknown.dll'
$outside = Join-Path $root 'outside.dll'
Set-Content -LiteralPath $clip -Value 'private recording'
Set-Content -LiteralPath $settings -Value '{"ReplayHotkey":"Shift+Z"}'
Set-Content -LiteralPath $unknown -Value 'not owned by installer'
Set-Content -LiteralPath $outside -Value 'must remain outside app directory'
Set-Content -LiteralPath (Join-Path $installed 'modified.dll') -Value 'locally modified resource'
$clipHash = (Get-FileHash -LiteralPath $clip).Hash
$settingsHash = (Get-FileHash -LiteralPath $settings).Hash
$outsideHash = (Get-FileHash -LiteralPath $outside).Hash

# Corrupt inventory entries must never delete recordings or escape the app folder.
$oldInventory = Join-Path $installed '.clipalka-files.txt'
Add-Content -LiteralPath $oldInventory -Value "$outsideHash|..\outside.dll"
Add-Content -LiteralPath $oldInventory -Value "$clipHash|Clips\keep.mp4"

Remove-Item -LiteralPath (Join-Path $payload 'obsolete.dll'), (Join-Path $payload 'modified.dll')
Set-Content -LiteralPath (Join-Path $payload 'version.txt') -Value 'new'
Set-Content -LiteralPath (Join-Path $payload 'new.dll') -Value 'new package dependency'
& (Join-Path $PSScriptRoot 'New-InstallManifest.ps1') -SourceDirectory $payload
$newSetup = Compile-TestInstaller '0.9.3' 'new-setup'
# No /DIR: upgrade must discover the original installation directory itself.
Invoke-CheckedProcess $newSetup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$evidence/upgrade.log`"")

if ((Get-Content -LiteralPath (Join-Path $installed 'version.txt')).Trim() -ne 'new') { throw 'Previous directory was not upgraded.' }
if (Test-Path -LiteralPath (Join-Path $installed 'obsolete.dll')) { throw 'Obsolete owned file was not removed.' }
if (!(Test-Path -LiteralPath (Join-Path $installed 'modified.dll'))) { throw 'Modified file was deleted.' }
if (!(Test-Path -LiteralPath (Join-Path $installed 'new.dll'))) { throw 'New dependency missing.' }
if (!(Test-Path -LiteralPath (Join-Path $installed 'Удалить CLIPALKA.lnk'))) { throw 'Friendly uninstaller shortcut missing.' }
if ((Get-ItemProperty -LiteralPath $registry).DisplayVersion -ne '0.9.3') { throw 'Installed version was not updated.' }
$uninstallers = @(Get-ChildItem -LiteralPath $installed -Filter 'unins*.exe')
if ($uninstallers.Count -ne 1) { throw 'Upgrade created duplicate uninstallers.' }
if ((Get-FileHash -LiteralPath $clip).Hash -ne $clipHash -or (Get-FileHash -LiteralPath $settings).Hash -ne $settingsHash) { throw 'User recordings/settings changed.' }
if ((Get-FileHash -LiteralPath $outside).Hash -ne $outsideHash) { throw 'Unsafe relative path escaped the installation folder.' }
if (!(Test-Path -LiteralPath $unknown)) { throw 'Unknown user file was deleted.' }

# Run the actual uninstall path and prove that user files survive.
Invoke-CheckedProcess $uninstallers[0].FullName @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$evidence/uninstall.log`"")
if (Test-Path -LiteralPath (Join-Path $installed 'CLIPALKA.exe')) { throw 'Uninstall left application executable behind.' }
if (Test-Path -LiteralPath $registry) { throw 'Uninstall registration remains.' }
if ((Get-FileHash -LiteralPath $clip).Hash -ne $clipHash -or (Get-FileHash -LiteralPath $settings).Hash -ne $settingsHash) { throw 'Uninstall removed user data.' }
if (!(Test-Path -LiteralPath $unknown)) { throw 'Uninstall removed unknown files.' }
'PASS: previous directory reused; one installation/uninstaller; obsolete owned file removed; changed/unknown files preserved; recordings/settings preserved by upgrade and uninstall; unsafe inventory paths rejected.' |
    Set-Content -LiteralPath (Join-Path $evidence 'result.txt')
Get-Content -LiteralPath (Join-Path $evidence 'result.txt')
