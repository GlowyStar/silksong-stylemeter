<#
.SYNOPSIS
Packs the Release build for Nexus Mods and GitHub.

.DESCRIPTION
Builds Release, runs the tests, checks that no developer tool got into the plugin, and writes
dist\Stylemeter-<version>.zip (BepInEx/plugins/Stylemeter/ with the plugin, the readme and the license) next to its
SHA-256 checksum. Unpacking the zip into the game folder installs the mod.
#>
param(
    [string] $GameDir
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$buildArguments = @{ Configuration = 'Release' }
if ($GameDir) { $buildArguments.GameDir = $GameDir }
& (Join-Path $PSScriptRoot 'build.ps1') @buildArguments
if ($LASTEXITCODE -ne 0) { throw "The build or the tests failed ($LASTEXITCODE)" }

[xml] $props = Get-Content (Join-Path $root 'src\Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw 'No <Version> in src\Directory.Build.props' }

$buildRoot = if ($env:STYLEMETER_BUILD_ROOT) { $env:STYLEMETER_BUILD_ROOT } else { Join-Path $root '.build' }
$plugin = Join-Path $buildRoot 'bin\Stylemeter\Release\netstandard2.1\Stylemeter.dll'
if (-not (Test-Path $plugin)) { throw "The plugin was not built: $plugin" }

# The Release build must hold none of the developer tools (the Stylemeter.Dev namespace).
$bytes = [System.IO.File]::ReadAllBytes($plugin)
$text = [System.Text.Encoding]::ASCII.GetString($bytes)
if ($text.Contains('Stylemeter.Dev')) { throw 'The Release plugin contains developer tools; build it with -c Release' }

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist "Stylemeter-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

# Entry names are written by hand: Compress-Archive in Windows PowerShell stores backslashes, which other unzip tools
# and mod managers read as part of one file name.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($file in @($plugin, (Join-Path $root 'README.md'), (Join-Path $root 'LICENSE'))) {
        $entryName = 'BepInEx/plugins/Stylemeter/' + (Split-Path $file -Leaf)
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file, $entryName, 'Optimal') |
            Out-Null
    }
}
finally {
    $archive.Dispose()
}

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$zip.sha256" -Value "$hash  Stylemeter-$version.zip" -Encoding ascii
Write-Host "Packed $zip"
Write-Host "SHA-256 $hash"
