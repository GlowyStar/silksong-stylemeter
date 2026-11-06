<#
.SYNOPSIS
Builds the plugin and runs the scoring tests.

.DESCRIPTION
Release is the build players get. Dev adds the sandbox save slot, the command bridge and the research logs.
-Install copies the plugin into the game's BepInEx\plugins\Stylemeter folder.

.EXAMPLE
./tools/build.ps1
./tools/build.ps1 -Configuration Dev -Install
./tools/build.ps1 -GameDir "D:\Games\Hollow Knight Silksong"
#>
param(
    [ValidateSet('Release', 'Dev')]
    [string] $Configuration = 'Release',
    [switch] $Install,
    [string] $GameDir
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

# The dotnet on PATH can be a runtime without any SDK; the per-user install is the usual other place.
function Find-Dotnet {
    $candidates = @()
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }
    $candidates += Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
    foreach ($candidate in $candidates) {
        if ((Test-Path $candidate) -and (& $candidate --list-sdks)) { return $candidate }
    }
    throw 'The .NET SDK was not found: install it from https://dotnet.microsoft.com/download'
}

$dotnet = Find-Dotnet
$buildArguments = @('build', (Join-Path $root 'src\Stylemeter'), '-c', $Configuration, '-nologo')
if ($Install) { $buildArguments += '-p:InstallToGame=true' }
if ($GameDir) { $buildArguments += "-p:GameDir=$GameDir" }

& $dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $dotnet run --project (Join-Path $root 'src\Stylemeter.Sim') -c Release -- test
exit $LASTEXITCODE
