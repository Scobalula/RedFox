[CmdletBinding()]
param(
    [string]$FeedPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts/nuget"),
    [string[]]$DependencySource = @("https://api.nuget.org/v3/index.json"),
    [string]$PackageId = "RedFox.Graphics3D.Formats",
    [switch]$BuildLocalDependencyFeed = $true
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path $PSScriptRoot -Parent
$statePath = Join-Path $PSScriptRoot "Graphics3D.PackageVersions.json"
$state = Get-Content $statePath -Raw | ConvertFrom-Json
$entry = $state.packages.PSObject.Properties[$PackageId]

if (-not $entry)
{
    throw "'$PackageId' is not a Graphics3D package tracked by $statePath."
}

$version = $entry.Value.lastPublishedVersion
if (-not $version)
{
    throw "Pack '$PackageId' before running this clean-consumer test."
}

$FeedPath = [System.IO.Path]::GetFullPath($FeedPath)
if (-not (Test-Path -LiteralPath (Join-Path $FeedPath "$PackageId.$version.nupkg")))
{
    throw "The local feed does not contain '$PackageId.$version.nupkg'."
}

$scratchPath = Join-Path ([System.IO.Path]::GetTempPath()) ("RedFoxGraphics3DConsumer-" + [System.Guid]::NewGuid())
$packagesPath = Join-Path $scratchPath ".packages"
$dependencyFeedPath = Join-Path $scratchPath "dependency-feed"
$dependencyArtifactsPath = Join-Path ([System.IO.Path]::GetTempPath()) ("RedFoxGraphics3DDependencyArtifacts-" + [System.Guid]::NewGuid())
$consumerArtifactsPath = Join-Path ([System.IO.Path]::GetTempPath()) ("RedFoxGraphics3DConsumerArtifacts-" + [System.Guid]::NewGuid())
New-Item -ItemType Directory -Path $scratchPath | Out-Null

try
{
    if ($BuildLocalDependencyFeed)
    {
        $dependencyProjects = @(
            "../../../CallOfFile/src/CallOfFile/CallOfFile.csproj",
            "RedFox/RedFox.csproj",
            "RedFox.IO/RedFox.IO.csproj",
            "RedFox.Graphics2D.Primitives/RedFox.Graphics2D.Primitives.csproj",
            "RedFox.Graphics2D.Codecs/RedFox.Graphics2D.Codecs.csproj",
            "RedFox.Graphics2D.BlockCompression/RedFox.Graphics2D.BlockCompression.csproj",
            "RedFox.Graphics2D/RedFox.Graphics2D.csproj"
        )

        New-Item -ItemType Directory -Path $dependencyFeedPath | Out-Null
        foreach ($dependencyProject in $dependencyProjects)
        {
            & dotnet pack (Join-Path $repositoryRoot $dependencyProject) --configuration Release --output $dependencyFeedPath -p:PackageVersion=1.0.0 -p:UseArtifactsOutput=true "-p:ArtifactsPath=$dependencyArtifactsPath"
            if ($LASTEXITCODE -ne 0)
            {
                throw "Packing the local dependency '$dependencyProject' for the clean-consumer test failed."
            }
        }
    }

    $sources = @($FeedPath)
    if ($BuildLocalDependencyFeed)
    {
        $sources += $dependencyFeedPath
    }
    $sources += $DependencySource
    $sourceXml = ($sources | ForEach-Object {
        "    <add key=`"$([System.Security.SecurityElement]::Escape($_))`" value=`"$([System.Security.SecurityElement]::Escape($_))`" />"
    }) -join [Environment]::NewLine

    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
$sourceXml
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $scratchPath "NuGet.Config")

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$PackageId" Version="$version" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $scratchPath "Graphics3DConsumer.csproj")

    $programSource = if ($PackageId -eq "RedFox.Graphics3D.Formats")
    {
@"
using RedFox.Graphics3D.Formats;

Console.WriteLine(BuiltInFormats.CreateDefaultManager().GetType().FullName);
"@
    }
    else
    {
@"
using RedFox.Graphics3D;

Console.WriteLine(new Scene("PackageConsumer").Name);
"@
    }

    $programSource | Set-Content -LiteralPath (Join-Path $scratchPath "Program.cs")

    Push-Location $scratchPath
    try
    {
        & dotnet restore --configfile "NuGet.Config" --packages $packagesPath -p:UseArtifactsOutput=true "-p:ArtifactsPath=$consumerArtifactsPath"
        if ($LASTEXITCODE -ne 0)
        {
            throw "Clean consumer restore failed. Supply a dependency source containing dependencies not produced by the local validation feed."
        }

        & dotnet run --no-restore -p:UseArtifactsOutput=true "-p:ArtifactsPath=$consumerArtifactsPath"
        if ($LASTEXITCODE -ne 0)
        {
            throw "Clean consumer build or run failed."
        }
    }
    finally
    {
        Pop-Location
    }
}
finally
{
    Remove-Item -LiteralPath $scratchPath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $dependencyArtifactsPath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $consumerArtifactsPath -Recurse -Force -ErrorAction SilentlyContinue
}
