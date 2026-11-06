<#
.SYNOPSIS
Packs the Release build for GitHub, Nexus Mods and mod managers.

.DESCRIPTION
Builds Release, runs the tests, checks that no developer tool got into the plugin, and writes three archives to
dist, each next to its SHA-256 checksum:
- Stylemeter-<version>.zip: BepInEx/plugins/Stylemeter/ with the plugin, a short README.txt and the license, for a
  game that has BepInEx 5 already;
- Stylemeter-<version>-with-BepInEx.zip: the same together with BepInEx 5 itself, unmodified, taken from the game
  folder given as -BepInExFrom (the build's game folder by default); unpacked into the game folder, it is ready to
  play;
- Stylemeter-<version>-thunderstore.zip: the package for Thunderstore and r2modman (manifest.json, icon.png,
  README.md and the plugin), which r2modman can also import as a local mod.
It prints the plugin's own SHA-256 as well: a build of the same commit with the SDK in global.json gives the same.
#>
param(
    [string] $GameDir,
    # A game folder with BepInEx 5 installed: its loader goes into the archive with BepInEx.
    [string] $BepInExFrom
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$buildArguments = @{ Configuration = 'Release' }
if ($GameDir) { $buildArguments.GameDir = $GameDir }
& (Join-Path $PSScriptRoot 'build.ps1') @buildArguments
if ($LASTEXITCODE -ne 0) { throw "The build or the tests failed ($LASTEXITCODE)" }

[xml] $props = Get-Content (Join-Path $root 'src\Directory.Build.props')
$group = $props.Project.PropertyGroup | Select-Object -First 1
$version = $group.Version
if (-not $version) { throw 'No <Version> in src\Directory.Build.props' }
if (-not $BepInExFrom) { $BepInExFrom = if ($GameDir) { $GameDir } else { $group.GameDir.InnerText } }

$buildRoot = if ($env:STYLEMETER_BUILD_ROOT) { $env:STYLEMETER_BUILD_ROOT } else { Join-Path $root '.build' }
$plugin = Join-Path $buildRoot 'bin\Stylemeter\Release\netstandard2.1\Stylemeter.dll'
if (-not (Test-Path $plugin)) { throw "The plugin was not built: $plugin" }

# The Release build must hold none of the developer tools (the Stylemeter.Dev namespace).
$bytes = [System.IO.File]::ReadAllBytes($plugin)
$text = [System.Text.Encoding]::ASCII.GetString($bytes)
if ($text.Contains('Stylemeter.Dev')) { throw 'The Release plugin contains developer tools; build it with -c Release' }

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$staging = Join-Path $dist 'staging'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force $staging | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding $false

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

# Entry names are written by hand: Compress-Archive in Windows PowerShell stores backslashes, which other unzip tools
# and mod managers read as part of one file name.
function New-Archive([string] $name, [object[]] $entries) {
    $path = Join-Path $dist $name
    if (Test-Path $path) { Remove-Item $path -Force }
    $archive = [System.IO.Compression.ZipFile]::Open($path, 'Create')
    try {
        foreach ($entry in $entries) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $entry.File, $entry.Name,
                'Optimal') | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
    $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -Path "$path.sha256" -Value "$hash  $name" -Encoding ascii
    Write-Host "Packed $name"
    Write-Host "  SHA-256 $hash"
}

$pluginEntries = @(
    @{ File = $plugin; Name = 'BepInEx/plugins/Stylemeter/Stylemeter.dll' },
    @{ File = (Join-Path $PSScriptRoot 'README.txt'); Name = 'BepInEx/plugins/Stylemeter/README.txt' },
    @{ File = (Join-Path $root 'LICENSE'); Name = 'BepInEx/plugins/Stylemeter/LICENSE' }
)
New-Archive "Stylemeter-$version.zip" $pluginEntries

# BepInEx as its release zip lays it out in the game folder: the doorstop next to the game's exe and BepInEx/core.
# Nothing else of that game folder goes in: no config, no other plugin, no log.
$loaderFiles = @('winhttp.dll', 'doorstop_config.ini', '.doorstop_version', 'changelog.txt')
$core = Join-Path $BepInExFrom 'BepInEx\core'
foreach ($file in $loaderFiles) {
    if (-not (Test-Path (Join-Path $BepInExFrom $file))) { throw "No BepInEx 5 in $BepInExFrom (missing $file)" }
}
if (-not (Test-Path (Join-Path $core 'BepInEx.dll'))) { throw "No BepInEx 5 in $BepInExFrom (missing BepInEx\core)" }
$bepInExVersion = (Get-Item (Join-Path $core 'BepInEx.dll')).VersionInfo.FileVersion
if (-not $bepInExVersion.StartsWith('5.')) { throw "BepInEx $bepInExVersion in $BepInExFrom is not version 5" }
$notice = Join-Path $staging 'LICENSE-BepInEx.txt'
$lgpl = Get-Content (Join-Path $PSScriptRoot 'bepinex\LICENSE-LGPL-2.1.txt') -Raw
$header = "BepInEx $bepInExVersion, https://github.com/BepInEx/BepInEx`n`n" +
    "Included unmodified. BepInEx is free software under the GNU Lesser General Public License 2.1, below; its`n" +
    "source code is at the address above.`n`n"
[System.IO.File]::WriteAllText($notice, $header + $lgpl, $utf8)

$loaderEntries = @($loaderFiles | ForEach-Object { @{ File = (Join-Path $BepInExFrom $_); Name = $_ } })
$loaderEntries += Get-ChildItem $core -File | ForEach-Object { @{ File = $_.FullName; Name = "BepInEx/core/$($_.Name)" } }
$loaderEntries += @{ File = $notice; Name = 'BepInEx/LICENSE-BepInEx.txt' }
New-Archive "Stylemeter-$version-with-BepInEx.zip" ($loaderEntries + $pluginEntries)

$manifest = [ordered] @{
    name = 'Stylemeter'
    version_number = $version
    website_url = 'https://github.com/GlowyStar/silksong-stylemeter'
    description = 'A style meter for Hollow Knight: Silksong: about 250 named bonuses and eight ranks from ' +
        'DREAMER to SILKSONG, with settings in Options.'
    dependencies = @('silksong_modding-BepInExPack_Silksong-1.0.3')
}
$manifestPath = Join-Path $staging 'manifest.json'
[System.IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json), $utf8)
New-Archive "Stylemeter-$version-thunderstore.zip" @(
    @{ File = $manifestPath; Name = 'manifest.json' },
    @{ File = (Join-Path $PSScriptRoot 'thunderstore\icon.png'); Name = 'icon.png' },
    @{ File = (Join-Path $PSScriptRoot 'thunderstore\README.md'); Name = 'README.md' },
    @{ File = $plugin; Name = 'Stylemeter.dll' },
    @{ File = (Join-Path $root 'LICENSE'); Name = 'LICENSE' }
)
Remove-Item $staging -Recurse -Force

$pluginHash = (Get-FileHash $plugin -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path (Join-Path $dist "Stylemeter-$version-plugin.sha256") -Value "$pluginHash  Stylemeter.dll" `
    -Encoding ascii
Write-Host "Plugin Stylemeter.dll $version (BepInEx $bepInExVersion bundled)"
Write-Host "  SHA-256 $pluginHash"
