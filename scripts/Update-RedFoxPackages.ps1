param(
    [Parameter(Mandatory = $true)]
    [string] $Path,

    [string] $Version,

    [switch] $Build
)

$ErrorActionPreference = 'Stop'

$Path = (Resolve-Path $Path).Path
$files = Get-ChildItem $Path -Recurse -File -Include *.csproj, *.props | Where-Object FullName -NotMatch '\\(bin|obj|\.vs|artifacts)\\'
$solution = Get-ChildItem $Path -Recurse -File -Include *.slnx, *.sln | Where-Object FullName -NotMatch '\\(bin|obj|\.vs|artifacts)\\' | Select-Object -First 1
$packages = @($files | Select-String -Pattern '<PackageReference\s+Include="(RedFox\.[^"]+)"' | ForEach-Object { $_.Matches[0].Groups[1].Value } | Sort-Object -Unique)

if (-not $solution) {
    throw "No solution found under $Path"
}

if (-not $packages) {
    Write-Host "No RedFox packages referenced under $Path"
    return
}

if (-not $Version) {
    $Version = (Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$($packages[0].ToLowerInvariant())/index.json").versions[-1]
    Write-Host "Latest RedFox version: $Version"
}

$centralPropertyPattern = '(<RedFoxPackageVersion\b[^>]*>)[^<]*(</RedFoxPackageVersion>)'
$solutionDirectory = Get-Item $solution.DirectoryName
$centralPropertyFile = $null
$searchDirectory = $solutionDirectory
while ($null -ne $searchDirectory -and $searchDirectory.FullName.StartsWith($Path, [System.StringComparison]::OrdinalIgnoreCase)) {
    $candidate = Join-Path $searchDirectory.FullName 'Directory.Packages.props'
    if (Test-Path -LiteralPath $candidate) {
        $candidateText = [System.IO.File]::ReadAllText($candidate)
        if ([System.Text.RegularExpressions.Regex]::IsMatch($candidateText, $centralPropertyPattern)) {
            $centralPropertyFile = $candidate
            break
        }
    }

    $searchDirectory = $searchDirectory.Parent
}

if ($centralPropertyFile) {
    $propsText = [System.IO.File]::ReadAllText($centralPropertyFile)
    $updatedProps = [System.Text.RegularExpressions.Regex]::Replace(
        $propsText,
        $centralPropertyPattern,
        [System.Text.RegularExpressions.MatchEvaluator] {
            param($match)
            $match.Groups[1].Value + $Version + $match.Groups[2].Value
        },
        [System.Text.RegularExpressions.RegexOptions]::None,
        [TimeSpan]::FromSeconds(2))

    $redFoxPackageVersionPattern = '(<PackageVersion\b(?=[^>]*\bInclude="RedFox\.[^"]+")(?=[^>]*\bVersion=")[^>]*\bVersion=")[^"]+("\s*/>)'
    $updatedProps = [System.Text.RegularExpressions.Regex]::Replace(
        $updatedProps,
        $redFoxPackageVersionPattern,
        [System.Text.RegularExpressions.MatchEvaluator] {
            param($match)
            $match.Groups[1].Value + '$(RedFoxPackageVersion)' + $match.Groups[2].Value
        },
        [System.Text.RegularExpressions.RegexOptions]::None,
        [TimeSpan]::FromSeconds(2))

    if ($updatedProps -cne $propsText) {
        [System.IO.File]::WriteAllText($centralPropertyFile, $updatedProps, [System.Text.UTF8Encoding]::new($false))
    }

    Write-Host "Set RedFoxPackageVersion in $centralPropertyFile to $Version"
}
else {
    $arguments = $packages | ForEach-Object { "$_@$Version" }
    & dotnet package update @arguments --project $solution.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet package update failed with exit code $LASTEXITCODE"
    }
}

Write-Host "Restoring $($solution.Name) with RedFox $Version from configured NuGet sources"
& dotnet restore $solution.FullName
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE"
}

if ($Build) {
    Write-Host "Building $($solution.Name) with RedFox $Version"
    & dotnet build $solution.FullName --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}

Write-Host "Updated $($solution.FullName) to RedFox $Version"
