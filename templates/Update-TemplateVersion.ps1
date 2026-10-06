param(
    [string] $Version
)

$ErrorActionPreference = 'Stop'

$templateConfig = (Resolve-Path (Join-Path $PSScriptRoot 'RedFox.GameExtraction.Tool\.template.config\template.json')).Path

if (-not $Version) {
    $Version = (Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/redfox.gameextraction/index.json").versions[-1]
}

$text = [System.IO.File]::ReadAllText($templateConfig)
$pattern = '("RedFoxVersion"\s*:\s*\{[^}]*?"defaultValue"\s*:\s*)"[^"]*"'
$updated = [System.Text.RegularExpressions.Regex]::Replace($text, $pattern, "`${1}`"$Version`"", [System.Text.RegularExpressions.RegexOptions]::None, [TimeSpan]::FromSeconds(2))

if ($updated -cne $text) {
    [System.IO.File]::WriteAllText($templateConfig, $updated, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Set template RedFoxVersion to $Version"
}
else {
    Write-Host "RedFoxVersion unchanged in $templateConfig"
}
