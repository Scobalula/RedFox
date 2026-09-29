param(
    [Parameter(Mandatory)]
    [string] $Path,

    [string] $Version
)

$ErrorActionPreference = 'Stop'

$files = Get-ChildItem $Path -Recurse -File -Include *.csproj, *.props | Where-Object FullName -NotMatch '\\(bin|obj|\.vs)\\'
$solution = Get-ChildItem $Path -Recurse -File -Include *.slnx, *.sln | Where-Object FullName -NotMatch '\\(bin|obj|\.vs)\\' | Select-Object -First 1
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

dotnet nuget locals http-cache --clear | Out-Null

$arguments = $packages | ForEach-Object { "$_@$Version" }

dotnet package update @arguments --project $solution.FullName

if ($LASTEXITCODE -ne 0) {
    throw "dotnet package update failed with exit code $LASTEXITCODE"
}
