param([string]$BuildDirectory = "$PSScriptRoot/../artifacts/apphost", [string]$FeedPath)

$ErrorActionPreference = 'Stop'
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$hostPacks = Join-Path (Split-Path (Get-Command dotnet).Source) 'packs/Microsoft.NETCore.App.Host.win-x64'
$hostPack = Get-ChildItem $hostPacks -Directory | Sort-Object { [version]($_.Name -replace '-.*$', '') } | Select-Object -Last 1
if (!$hostPack) { throw 'Microsoft.NETCore.App.Host.win-x64 pack was not found.' }
$native = Join-Path $hostPack.FullName 'runtimes/win-x64/native'
$build = Join-Path $BuildDirectory 'launcher'
cmake -S (Join-Path $PSScriptRoot 'launcher') -B $build -A x64 "-DDOTNET_HOST_PACK=$native"
if ($LASTEXITCODE -ne 0) { throw 'CMake configuration failed.' }
cmake --build $build --config Release
if ($LASTEXITCODE -ne 0) { throw 'Launcher build failed.' }
$packer = Join-Path $BuildDirectory 'packer'
dotnet build (Join-Path $PSScriptRoot 'RedFox.AppHost.Packer') -c Release -o $packer
if ($LASTEXITCODE -ne 0) { throw 'Packer build failed.' }
Copy-Item -LiteralPath (Join-Path $build 'Release/RedFoxAppHost.exe') -Destination (Join-Path $packer 'RedFoxAppHost.exe')
if ($FeedPath) {
    $commit = (git -C $PSScriptRoot rev-parse --short HEAD).Trim()
    $version = "$([DateTime]::UtcNow.ToString('yyyy.M.d'))-local.g$commit.t$(Get-Date -Format 'yyyyMMddHHmmss')"
    dotnet pack (Join-Path $PSScriptRoot 'RedFox.AppHost') -c Release -o $FeedPath "-p:Version=$version"
    if ($LASTEXITCODE -ne 0) { throw 'Package pack failed.' }
    Write-Host "RedFox.AppHost $version packed into $FeedPath"
}
