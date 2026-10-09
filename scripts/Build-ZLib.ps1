param([string]$BuildDirectory = "$PSScriptRoot/../artifacts/zlib")

$ErrorActionPreference = 'Stop'
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
New-Item -ItemType Directory -Force -Path $BuildDirectory | Out-Null
$archive = Join-Path $BuildDirectory 'zlib-1.3.2.tar.gz'
Invoke-WebRequest 'https://zlib.net/zlib-1.3.2.tar.gz' -OutFile $archive
if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne 'bb329a0a2cd0274d05519d61c667c062e06990d72e125ee2dfa8de64f0119d16') {
    throw 'zlib source checksum mismatch.'
}
tar -xf $archive -C $BuildDirectory
if ($LASTEXITCODE -ne 0) { throw 'Source extraction failed.' }
$source = Join-Path $BuildDirectory 'zlib-1.3.2'
$build = Join-Path $BuildDirectory 'build'
cmake -S $source -B $build -A x64 -DZLIB_BUILD_TESTING=OFF -DZLIB_BUILD_STATIC=OFF -DZLIB_BUILD_SHARED=ON
if ($LASTEXITCODE -ne 0) { throw 'CMake configuration failed.' }
cmake --build $build --config Release
if ($LASTEXITCODE -ne 0) { throw 'zlib build failed.' }
$native = Join-Path $PSScriptRoot '../src/cs/RedFox.Compression.Deflate/runtimes/win-x64/native'
New-Item -ItemType Directory -Force -Path $native | Out-Null
$dll = Get-ChildItem $build -Recurse -Filter z.dll | Select-Object -First 1
if (!$dll) { throw 'z.dll was not produced.' }
Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $native 'zlib1.dll')
Copy-Item -LiteralPath (Join-Path $source 'LICENSE') -Destination (Join-Path $PSScriptRoot '../licenses/ZLib-LICENSE')
