param(
    [string] $FeedPath = 'G:\Nuget',

    [string] $SourceName = 'LocalNuGet'
)

$ErrorActionPreference = 'Stop'

$redFoxRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solutionPath = Join-Path $redFoxRoot 'src\cs\RedFox.slnx'
if (-not (Test-Path -LiteralPath $solutionPath)) {
    throw "RedFox solution not found: $solutionPath"
}

if ([string]::IsNullOrWhiteSpace($env:APPDATA)) {
    throw 'APPDATA is not set; cannot register the local feed in the Windows user NuGet configuration.'
}

$FeedPath = [System.IO.Path]::GetFullPath($FeedPath)
New-Item -ItemType Directory -Force -Path $FeedPath | Out-Null
$FeedPath = (Resolve-Path $FeedPath).Path

$commit = (& git -C $redFoxRoot rev-parse --short HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Could not read the RedFox git revision from $redFoxRoot"
}

$dateVersion = [DateTime]::UtcNow.ToString('yyyy.M.d')
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$version = "$dateVersion-local.g$commit.t$stamp"

Write-Host "Packing RedFox as $version into $FeedPath"
& dotnet pack $solutionPath --configuration Release --output $FeedPath "-p:Version=$version"
if ($LASTEXITCODE -ne 0) {
    throw "RedFox pack failed with exit code $LASTEXITCODE"
}

$userConfigPath = Join-Path $env:APPDATA 'NuGet\NuGet.Config'
$userConfigDirectory = Split-Path -Parent $userConfigPath
New-Item -ItemType Directory -Force -Path $userConfigDirectory | Out-Null

function Ensure-NuGetSource {
    param(
        [Parameter(Mandatory = $true)] [string] $Name,
        [Parameter(Mandatory = $true)] [string] $Source
    )

    $hasSource = $false
    if (Test-Path -LiteralPath $userConfigPath) {
        [xml] $config = Get-Content -LiteralPath $userConfigPath
        $keys = @($config.configuration.packageSources.add | ForEach-Object { $_.key })
        $hasSource = $keys -contains $Name
    }

    if ($hasSource) {
        & dotnet nuget update source $Name --source $Source --configfile $userConfigPath
    }
    else {
        & dotnet nuget add source $Source --name $Name --configfile $userConfigPath
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Could not register NuGet source '$Name' at '$Source' (exit code $LASTEXITCODE)"
    }
}

Ensure-NuGetSource -Name 'nuget.org' -Source 'https://api.nuget.org/v3/index.json'
Ensure-NuGetSource -Name $SourceName -Source $FeedPath

Write-Host "Registered '$SourceName' in $userConfigPath"
Write-Host "Local RedFox package version: $version"
Write-Host "Use this version with scripts\Update-RedFoxPackages.ps1 -Path <consumer-project> -Version `"$version`"."
