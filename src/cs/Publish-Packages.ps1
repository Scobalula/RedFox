[CmdletBinding()]
param(
    [string]$Source = "Development"
)

$ErrorActionPreference = "Stop"

$versionPath = Join-Path $PSScriptRoot "..\..\build-version.txt"
$outputPath = Join-Path $PSScriptRoot "artifacts\packages"
$callOfFileProject = Join-Path $PSScriptRoot "..\..\..\CallOfFile\src\CallOfFile\CallOfFile.csproj"

$today = [DateTime]::UtcNow.ToString("yyyy.M.d")
$lastVersion = if (Test-Path $versionPath) { (Get-Content $versionPath -Raw).Trim() } else { "" }
$buildNumber = if ($lastVersion -match "^$([regex]::Escape($today))\.(\d+)$") { [int]$Matches[1] + 1 } else { 1 }
$version = "$today.$buildNumber"

Write-Host "Publishing RedFox $version to $Source" -ForegroundColor Cyan

if (Test-Path $outputPath) { Remove-Item $outputPath -Recurse -Force }

dotnet pack (Join-Path $PSScriptRoot "RedFox.sln") -c Release -p:Version=$version -o $outputPath
if ($LASTEXITCODE -ne 0) { throw "Packing RedFox.sln failed." }

dotnet pack $callOfFileProject -c Release -p:Version=$version -o $outputPath
if ($LASTEXITCODE -ne 0) { throw "Packing CallOfFile failed." }

foreach ($package in Get-ChildItem $outputPath -Filter "*.nupkg")
{
    dotnet nuget push $package.FullName --source $Source --skip-duplicate
    if ($LASTEXITCODE -ne 0) { throw "Pushing $($package.Name) failed." }
}

Set-Content $versionPath $version -NoNewline

Write-Host "Published RedFox $version" -ForegroundColor Green
