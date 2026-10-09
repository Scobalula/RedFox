param([string]$BuildDirectory = "$PSScriptRoot/../artifacts/gdeflate")

$ErrorActionPreference = 'Stop'
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$source = Join-Path $BuildDirectory 'source'
$build = Join-Path $BuildDirectory 'build'
$directStorageRevision = 'c53f1499d5f67a61b69a1a348d22dcd2b4cb4ede'

New-Item -ItemType Directory -Force -Path $BuildDirectory | Out-Null
if (!(Test-Path (Join-Path $source '.git'))) {
    New-Item -ItemType Directory -Force -Path $source | Out-Null
    git -C $source init
    if ($LASTEXITCODE -ne 0) { throw 'Git initialization failed.' }
    git -C $source remote add origin https://github.com/microsoft/DirectStorage.git
    if ($LASTEXITCODE -ne 0) { throw 'Git remote setup failed.' }
}

git -C $source fetch --depth 1 origin $directStorageRevision
if ($LASTEXITCODE -ne 0) { throw 'DirectStorage source download failed.' }
git -C $source checkout --force --detach FETCH_HEAD
if ($LASTEXITCODE -ne 0) { throw 'DirectStorage source checkout failed.' }
git -C $source submodule update --init --depth 1 -- GDeflate/3rdparty/libdeflate
if ($LASTEXITCODE -ne 0) { throw 'GDeflate dependency download failed.' }

$libdeflate = Join-Path $source 'GDeflate/3rdparty/libdeflate'
cmake -S (Join-Path $PSScriptRoot 'GDeflate') -B $build -A x64 "-DLIBDEFLATE_SOURCE=$libdeflate"
if ($LASTEXITCODE -ne 0) { throw 'CMake configuration failed.' }
cmake --build $build --config Release --target gdeflate
if ($LASTEXITCODE -ne 0) { throw 'GDeflate build failed.' }

$native = Join-Path $PSScriptRoot '../src/cs/RedFox.Compression.GDeflate/runtimes/win-x64/native'
New-Item -ItemType Directory -Force -Path $native | Out-Null
$dll = Get-ChildItem -LiteralPath $build -Recurse -Filter libgdeflate.dll | Select-Object -First 1
if (!$dll) { throw 'libgdeflate.dll was not produced.' }
Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $native 'libgdeflate.dll')
