param([Parameter(Mandatory)][string]$SourceDirectory)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $SourceDirectory).Path
$manifest = Join-Path $root '.clipalka-files.txt'
$entries = Get-ChildItem -LiteralPath $root -File -Recurse |
    Where-Object { $_.FullName -ne $manifest } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($root, $_.FullName).Replace('/', '\')
        if ($relative.Contains('|')) { throw "Unsupported package file name: $relative" }
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        "$hash|$relative"
    }
# ASCII is not sufficient for localized resource directories; Inno reads UTF-8 BOM.
[IO.File]::WriteAllLines($manifest, [string[]]$entries, [Text.UTF8Encoding]::new($true))
