param([string]$BuildDirectory = "$PSScriptRoot/../artifacts/apphost/launcher")

$ErrorActionPreference = 'Stop'
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$hostPacks = Join-Path (Split-Path (Get-Command dotnet).Source) 'packs/Microsoft.NETCore.App.Host.win-x64'
$hostPack = Get-ChildItem $hostPacks -Directory | Sort-Object { [version]($_.Name -replace '-.*$', '') } | Select-Object -Last 1
if (!$hostPack) { throw 'Microsoft.NETCore.App.Host.win-x64 pack was not found.' }
$native = Join-Path $hostPack.FullName 'runtimes/win-x64/native'
cmake -S (Join-Path $PSScriptRoot 'AppHost') -B $BuildDirectory -A x64 "-DDOTNET_HOST_PACK=$native"
if ($LASTEXITCODE -ne 0) { throw 'CMake configuration failed.' }
cmake --build $BuildDirectory --config Release
if ($LASTEXITCODE -ne 0) { throw 'Launcher build failed.' }
$output = Join-Path $PSScriptRoot '../src/cs/RedFox.AppHost.Packer/runtimes/win-x64/native'
New-Item -ItemType Directory -Force -Path $output | Out-Null
Copy-Item -LiteralPath (Join-Path $BuildDirectory 'Release/RedFoxAppHost.exe') -Destination (Join-Path $output 'RedFoxAppHost.exe')
Copy-Item -LiteralPath (Join-Path $BuildDirectory '_deps/brotli-src/LICENSE') -Destination (Join-Path $PSScriptRoot '../licenses/Brotli-LICENSE')
