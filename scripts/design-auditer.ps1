param(
    [string]$Base = "HEAD"
)

$ErrorActionPreference = "Stop"
$findings = [System.Collections.Generic.List[object]]::new()
$changedLines = @{}
$manualReview = @(
    'Simplicity, scope, behavior preservation, and whether a helper or abstraction is justified.',
    'Whether multiline expressions have a genuine readability reason and align with neighbouring code.',
    'Class member ordering, one significant type per file, and whether private state is unused.',
    'Whether comments or XML summaries explain obvious code or tell an unnecessary story.',
    'Whether the implementation has redundant validation, silent fallbacks, or broad exception handling.',
    'Whether public behavior and APIs are preserved and the diff is focused.',
    'Whether standard library functionality is preferred over custom utilities or dependencies.',
    'Whether methods are focused, abstractions are justified, and tests or build checks are relevant.'
)
$patterns = @(
    @{ Pattern = '(?<!\?)!(?=[.\[);,])'; Rule = 'null-forgiving operator candidate' },
    @{ Pattern = '///\s*<summary>.*</summary>'; Rule = 'single-line XML summary' },
    @{ Pattern = '/\*.*\*/'; Rule = 'inline block comment candidate' },
    @{ Pattern = '^\s*//(?!/)'; Rule = 'comment candidate' },
    @{ Pattern = '\b(?:public|private|protected|internal)\s+[^;{}=]+\([^)]*=\s*[^)]*\)'; Rule = 'default parameter candidate' },
    @{ Pattern = '\b(?:class|struct|interface)\s+\w*(?:Core|Helper)\b'; Rule = 'generic type name candidate' }
)

function Add-Candidates([string]$File, [int]$Line, [string]$Text) {
    $ternaryCount = [regex]::Matches($Text, '\s\?(?![?.])').Count
    if ($ternaryCount -gt 1 -and [regex]::Matches($Text, ':').Count -ge $ternaryCount) {
        $findings.Add([pscustomobject]@{ File = $File; Line = $Line; Rule = 'nested ternary candidate'; Text = $Text.Trim() })
    }
    foreach ($rule in $patterns) {
        if ($Text -match $rule.Pattern) {
            $findings.Add([pscustomobject]@{ File = $File; Line = $Line; Rule = $rule.Rule; Text = $Text.Trim() })
        }
    }
    if ($Text -match '\bnew\s+[\w<>]+\s*\{[^}]*,') {
        $findings.Add([pscustomobject]@{ File = $File; Line = $Line; Rule = 'compact object initializer candidate'; Text = $Text.Trim() })
    }
    if ($Text -match '^\s*[^/].*(?:=>|=|\.|\+|\-|\*|/|&&|\|\|)\s*$') {
        $findings.Add([pscustomobject]@{ File = $File; Line = $Line; Rule = 'split expression candidate'; Text = $Text.Trim() })
    }
    if ($Text.Length -gt 200) {
        $findings.Add([pscustomobject]@{ File = $File; Line = $Line; Rule = 'long line candidate'; Text = $Text.Trim() })
    }
    if ($Text -match '^\s*(?:if|else|for|foreach|while)\b.*\S\s*$' -and $Text -notmatch '[{};]\s*$' -and $Text -notmatch '^\s*else\s*$') {
        $findings.Add([pscustomobject]@{ File = $File; Line = $Line; Rule = 'control statement brace candidate'; Text = $Text.Trim() })
    }
    if ($Text -match '\s+$') {
        $findings.Add([pscustomobject]@{ File = $File; Line = $Line; Rule = 'trailing whitespace'; Text = $Text.TrimEnd() })
    }
}

function Get-VisualColumn([string]$Text, [int]$Length) {
    $column = 0
    for ($index = 0; $index -lt $Length; $index++) {
        if ($Text[$index] -eq "`t") {
            $column += 4 - ($column % 4)
        } else {
            $column++
        }
    }
    return $column
}

function Report-AssignmentGroup([string]$File, [System.Collections.Generic.List[object]]$Group, [string]$Rule = 'assignment alignment candidate', [bool]$RequireChangedLine = $true) {
    if ($Group.Count -lt 2) {
        return
    }
    if ($RequireChangedLine) {
        $changed = $false
        foreach ($item in $Group) {
            if ($changedLines[$File].Contains($item.Line)) {
                $changed = $true
                break
            }
        }
        if (-not $changed) {
            return
        }
    }
    $columns = @($Group | ForEach-Object Column | Select-Object -Unique)
    $usesSpacesForAlignment = @($Group | Where-Object { $_.Gap.Length -gt 1 -and $_.Gap -notmatch "`t" }).Count -gt 0
    if ($columns.Count -gt 1 -or $usesSpacesForAlignment) {
        foreach ($item in $Group) {
            $findings.Add([pscustomobject]@{ File = $File; Line = $item.Line; Rule = $Rule; Text = $item.Text.Trim() })
        }
    }
}

$currentFile = ''
$currentLine = 0
foreach ($line in git diff --unified=0 $Base -- '*.cs') {
    if ($line -match '^\+\+\+ b/(.+)$') {
        $currentFile = $Matches[1]
        continue
    }
    if ($line -match '^@@ .* \+(\d+)(?:,(\d+))? @@') {
        $currentLine = [int]$Matches[1]
        continue
    }
    if ($line.StartsWith('+++') -or $line.StartsWith('---')) {
        continue
    }
    if ($line.StartsWith('+')) {
        Add-Candidates $currentFile $currentLine $line.Substring(1)
        if (-not $changedLines.ContainsKey($currentFile)) {
            $changedLines[$currentFile] = [System.Collections.Generic.HashSet[int]]::new()
        }
        $changedLines[$currentFile].Add($currentLine) | Out-Null
        $currentLine++
        continue
    }
    if (-not $line.StartsWith('-')) {
        $currentLine++
    }
}

foreach ($file in git ls-files --others --exclude-standard -- '*.cs') {
    $changedLines[$file] = [System.Collections.Generic.HashSet[int]]::new()
    $lineNumber = 0
    foreach ($text in Get-Content -LiteralPath $file) {
        $lineNumber++
        $changedLines[$file].Add($lineNumber) | Out-Null
        Add-Candidates $file $lineNumber $text
    }
}

foreach ($file in $changedLines.Keys) {
    $group = [System.Collections.Generic.List[object]]::new()
    $groupLineNumber = 0
    foreach ($text in Get-Content -LiteralPath $file) {
        $lineNumber = $groupLineNumber + 1
        $groupLineNumber = $lineNumber
        $matched = $text -match '^(\s*)(var|[A-Za-z_][\w<>,?.\[\]]*)\s+([A-Za-z_]\w*)\s*='
        if ($matched) {
            $indent = $Matches[1]
            $type = $Matches[2]
            $declaration = [regex]::Match($text, '^(\s*)(var|[A-Za-z_][\w<>,?.\[\]]*)\s+([A-Za-z_]\w*)\s*=')
            $nameEnd = $declaration.Groups[3].Index + $declaration.Groups[3].Length
            if ($group.Count -gt 0 -and $group[0].Indent -ne $indent) {
                Report-AssignmentGroup $file $group
                $group.Clear()
            }
        }
        if ($matched) {
            $equals = $text.IndexOf('=')
            $group.Add([pscustomobject]@{ Line = $lineNumber; Indent = $indent; Type = $type; Column = Get-VisualColumn $text $equals; Gap = $text.Substring($nameEnd, $equals - $nameEnd); Text = $text })
        } else {
            Report-AssignmentGroup $file $group
            $group.Clear()
        }
    }
    Report-AssignmentGroup $file $group
}

foreach ($file in $changedLines.Keys) {
    $lines = @(Get-Content -LiteralPath $file)
    $namespaceLine = $lines | Where-Object { $_ -match '^\s*namespace\b' } | Select-Object -First 1
    $topLevelIndent = ''
    if ($namespaceLine -and $namespaceLine -notmatch ';\s*$') {
        $topLevelIndent = [regex]::Match($namespaceLine, '^\s*').Value + '    '
    } elseif ($namespaceLine) {
        $topLevelIndent = [regex]::Match($namespaceLine, '^\s*').Value
    }
    $topLevelTypes = [System.Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt $lines.Count; $index++) {
        $typeMatch = [regex]::Match($lines[$index], '^(\s*)(?:(?:public|internal|private|protected|static|abstract|sealed|partial)\s+)*(class|struct|interface)\s+(\w+)')
        if (-not $typeMatch.Success) {
            continue
        }
        if ($typeMatch.Groups[1].Value -eq $topLevelIndent) {
            $topLevelTypes.Add([pscustomobject]@{ Line = $index + 1; Name = $typeMatch.Groups[3].Value })
        } else {
            $findings.Add([pscustomobject]@{ File = $file; Line = $index + 1; Rule = 'nested type candidate'; Text = $lines[$index].Trim() })
        }
    }
    if ($topLevelTypes.Count -gt 1) {
        foreach ($type in $topLevelTypes) {
            $findings.Add([pscustomobject]@{ File = $file; Line = $type.Line; Rule = 'multiple significant types per file candidate'; Text = $type.Name })
        }
    }

    $className = ''
    $classIndent = ''
    $memberOrder = 0
    $classChanged = $false
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match '^\s*(?:(?:public|internal|private|protected|static|abstract|sealed|partial)\s+)*class\s+(\w+)') {
            $className = $Matches[1]
            $classIndent = [regex]::Match($lines[$index], '^\s*').Value
            $memberOrder = 0
            $classChanged = $false
            continue
        }
        if (-not $className) {
            continue
        }
        $lineNumber = $index + 1
        $memberRank = -1
        if ($lines[$index] -match '^\s*private\s+(?:static\s+)?(?:readonly\s+)?[\w<>,?.\[\]]+\s+\w+\s*(?:=|;)') { $memberRank = 0 }
        elseif ($lines[$index] -match '^\s*protected\s+[^ (]+\s+\w+\s*\{') { $memberRank = 1 }
        elseif ($lines[$index] -match '^\s*public\s+[^ (]+\s+\w+\s*\{') { $memberRank = 2 }
        elseif ($lines[$index] -match "^\s*public\s+$([regex]::Escape($className))\s*\(") { $memberRank = 3 }
        elseif ($lines[$index] -match '^\s*public\s+[^=;{}]+\([^;]*\)') { $memberRank = 4 }
        elseif ($lines[$index] -match '^\s*private\s+[^=;{}]+\([^;]*\)') { $memberRank = 5 }
        if ($memberRank -ge 0) {
            if ($changedLines[$file].Contains($lineNumber)) { $classChanged = $true }
            if ($memberRank -lt $memberOrder -and ($classChanged -or $changedLines[$file].Contains($lineNumber))) {
                $findings.Add([pscustomobject]@{ File = $file; Line = $lineNumber; Rule = 'class member order candidate'; Text = $lines[$index].Trim() })
            }
            $memberOrder = [Math]::Max($memberOrder, $memberRank)
        }
        if ($lines[$index] -match '^\s*}\s*$' -and [regex]::Match($lines[$index], '^\s*').Value -eq $classIndent) {
            $className = ''
        }
    }
}

foreach ($file in $changedLines.Keys) {
    $group = [System.Collections.Generic.List[object]]::new()
    $lineNumber = 0
    $insideSwitch = $false
    foreach ($text in Get-Content -LiteralPath $file) {
        $lineNumber++
        if (-not $insideSwitch -and $text -match '\bswitch\s*(?:\{\s*)?$') {
            $insideSwitch = $true
            continue
        }
        if ($insideSwitch -and $text -match '^\s*{\s*$') {
            continue
        }
        if ($insideSwitch -and $text -match '^\s*}\s*[,;)]?\s*$') {
            Report-AssignmentGroup $file $group 'switch arm alignment candidate' $false
            $group.Clear()
            $insideSwitch = $false
            continue
        }
        if ($insideSwitch) {
            $arm = [regex]::Match($text, '^(\s*)(.+?)\s+=>')
            if ($arm.Success) {
                $indent = $arm.Groups[1].Value
                if ($group.Count -gt 0 -and $group[0].Indent -ne $indent) {
                    Report-AssignmentGroup $file $group 'switch arm alignment candidate' $false
                    $group.Clear()
                }
                $arrow = $text.IndexOf('=>')
                $gap = $text.Substring($arm.Groups[2].Index + $arm.Groups[2].Length, $arrow - ($arm.Groups[2].Index + $arm.Groups[2].Length))
                $group.Add([pscustomobject]@{ Line = $lineNumber; Indent = $indent; Column = Get-VisualColumn $text $arrow; Gap = $gap; Text = $text })
            } else {
                Report-AssignmentGroup $file $group 'switch arm alignment candidate' $false
                $group.Clear()
            }
            continue
        }
        $assignment = [regex]::Match($text, '^(\s*)([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*(?:\[[^\]]+\])?)\s*=(?!=|>)')
        if ($assignment.Success) {
            $indent = $assignment.Groups[1].Value
            if ($group.Count -gt 0 -and $group[0].Indent -ne $indent) {
                Report-AssignmentGroup $file $group
                $group.Clear()
            }
            $leftEnd = $assignment.Groups[2].Index + $assignment.Groups[2].Length
            $equals = $text.IndexOf('=')
            $group.Add([pscustomobject]@{ Line = $lineNumber; Indent = $indent; Column = Get-VisualColumn $text $equals; Gap = $text.Substring($leftEnd, $equals - $leftEnd); Text = $text })
        } else {
            Report-AssignmentGroup $file $group
            $group.Clear()
        }
    }
    Report-AssignmentGroup $file $group
}

if ($findings.Count -eq 0) {
    Write-Output 'No automated candidates found in the changed C# scope.'
} else {
    $findings | Sort-Object File, Line, Rule | ForEach-Object { Write-Output "$($_.File):$($_.Line) [$($_.Rule)] $($_.Text)" }
    Write-Output "`n$($findings.Count) design candidate(s). Review each finding in context."
}

Write-Output "`nManual review items that depend on behavior, intent, or readability judgment:"
$manualReview | ForEach-Object { Write-Output "- $_" }
