[CmdletBinding()]
param(
    [string[]]$PackageId,
    [string]$FeedPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts/nuget")
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path $PSScriptRoot -Parent
$statePath = Join-Path $PSScriptRoot "Graphics3D.PackageVersions.json"
$state = Get-Content $statePath -Raw | ConvertFrom-Json
$packages = $state.packages
$allPackageIds = @($packages.PSObject.Properties.Name)

if (-not $PackageId -or $PackageId.Count -eq 0)
{
    $PackageId = $allPackageIds
}

foreach ($id in $PackageId)
{
    if ($id -notin $allPackageIds)
    {
        throw "'$id' is not a Graphics3D package tracked by $statePath."
    }
}

$selectedPackageIds = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]$PackageId,
    [System.StringComparer]::OrdinalIgnoreCase)
$versions = @{}
$today = Get-Date
$todayPrefix = "{0}.{1}.{2}" -f $today.Year, $today.Month, $today.Day

foreach ($id in $allPackageIds)
{
    $entry = $packages.PSObject.Properties[$id].Value
    $lastVersion = $entry.lastPublishedVersion

    if ($selectedPackageIds.Contains($id))
    {
        if ($lastVersion -and $lastVersion -match "^(?<date>\d{4}\.\d{1,2}\.\d{1,2})\.(?<build>\d+)$" -and $Matches.date -eq $todayPrefix)
        {
            $versions[$id] = "$todayPrefix.$([int]$Matches.build + 1)"
        }
        else
        {
            $versions[$id] = "$todayPrefix.1"
        }
    }
    elseif ($lastVersion)
    {
        $versions[$id] = $lastVersion
    }
}

foreach ($id in $PackageId)
{
    $entry = $packages.PSObject.Properties[$id].Value
    foreach ($dependencyId in $entry.dependencies)
    {
        if (-not $versions.ContainsKey($dependencyId))
        {
            throw "Packing '$id' requires the already published '$dependencyId' package, or include it in -PackageId."
        }
    }
}

$FeedPath = [System.IO.Path]::GetFullPath($FeedPath)
New-Item -ItemType Directory -Path $FeedPath -Force | Out-Null

foreach ($id in $PackageId)
{
    $packagePath = Join-Path $FeedPath "$id.$($versions[$id]).nupkg"
    if (Test-Path -LiteralPath $packagePath)
    {
        throw "Refusing to overwrite existing package '$packagePath'."
    }
}

$stagingPath = Join-Path ([System.IO.Path]::GetTempPath()) ("RedFoxGraphics3DPack-" + [System.Guid]::NewGuid())
New-Item -ItemType Directory -Path $stagingPath | Out-Null

try
{
    $versionProperties = @()
    foreach ($id in $allPackageIds)
    {
        if ($versions.ContainsKey($id))
        {
            $entry = $packages.PSObject.Properties[$id].Value
            $versionProperties += "-p:$($entry.versionProperty)=$($versions[$id])"
        }
    }

    foreach ($id in $PackageId)
    {
        $entry = $packages.PSObject.Properties[$id].Value
        $projectPath = Join-Path $repositoryRoot $entry.project
        $packArguments = @(
            "pack",
            $projectPath,
            "--configuration",
            "Release",
            "--output",
            $stagingPath,
            "-p:UseArtifactsOutput=true",
            "-p:ArtifactsPath=$stagingPath/artifacts"
        ) + $versionProperties

        & dotnet @packArguments
        if ($LASTEXITCODE -ne 0)
        {
            throw "Packing '$id' failed."
        }

        $stagedPackagePath = Join-Path $stagingPath "$id.$($versions[$id]).nupkg"
        if (-not (Test-Path -LiteralPath $stagedPackagePath))
        {
            throw "Packing '$id' did not create '$stagedPackagePath'."
        }
    }

    foreach ($id in $PackageId)
    {
        $stagedPackagePath = Join-Path $stagingPath "$id.$($versions[$id]).nupkg"
        Copy-Item -LiteralPath $stagedPackagePath -Destination $FeedPath -ErrorAction Stop

        $stagedSymbolsPath = Join-Path $stagingPath "$id.$($versions[$id]).snupkg"
        if (Test-Path -LiteralPath $stagedSymbolsPath)
        {
            Copy-Item -LiteralPath $stagedSymbolsPath -Destination $FeedPath -ErrorAction Stop
        }

        $packages.PSObject.Properties[$id].Value.lastPublishedVersion = $versions[$id]
    }

    $temporaryStatePath = "$statePath.tmp"
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $temporaryStatePath -NoNewline
    Move-Item -LiteralPath $temporaryStatePath -Destination $statePath -Force
}
finally
{
    Remove-Item -LiteralPath $stagingPath -Recurse -Force -ErrorAction SilentlyContinue
}
