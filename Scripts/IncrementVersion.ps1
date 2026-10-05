param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath
)

try {
    Write-Host "Loading project: $ProjectPath"

    [xml]$xml = Get-Content $ProjectPath -Encoding UTF8

    # Find PropertyGroup with ApplicationVersion
    $versionNode = $null
    foreach ($pg in $xml.Project.PropertyGroup) {
        if ($pg.ApplicationVersion) {
            $versionNode = $pg
            break
        }
    }

    if (-not $versionNode) {
        Write-Error "ApplicationVersion element not found in project file"
        exit 1
    }

    # Parse current ApplicationVersion (integer build number)
    $currentVersionText = $versionNode.ApplicationVersion.Trim()
    Write-Host "Current ApplicationVersion (raw): '$currentVersionText'"

    if ($currentVersionText -match '^(\d+)' ) {
        [int]$currentVersion = [int]$matches[1]
    }
    else {
        Write-Error "ApplicationVersion contains non-numeric value: '$currentVersionText'. Expected an integer."
        Write-Host "Please manually fix ApplicationVersion in the .csproj file to be a valid number." -ForegroundColor Yellow
        exit 1
    }

    [int]$newVersion = $currentVersion + 1
    $newVersionFormatted = $newVersion.ToString()

    # Parse ApplicationDisplayVersion — handles two formats:
    #   Prefix-only : "1.0."     → use CURRENT ApplicationVersion → "1.0.14.0"
    #   Full format : "1.0.14.0" → use INCREMENTED ApplicationVersion → "1.0.15.0"
    $currentDisplayVersion = $versionNode.ApplicationDisplayVersion.Trim()
    Write-Host "Current ApplicationDisplayVersion (raw): '$currentDisplayVersion'"

    if ($currentDisplayVersion -match '^(\d+\.\d+)\.$') {
        # Prefix-only format (e.g. "1.0.") — combine with current build number
        $basePrefix = $Matches[1]
        $newDisplayVersion = "$basePrefix.$currentVersion.0"
    }
    elseif ($currentDisplayVersion -match '^(\d+\.\d+)') {
        # Full format (e.g. "1.0.14.0") — use incremented build number
        $basePrefix = $Matches[1]
        $newDisplayVersion = "$basePrefix.$newVersion.0"
    }
    else {
        Write-Error "ApplicationDisplayVersion has unexpected format: '$currentDisplayVersion'."
        exit 1
    }

    Write-Host "Current ApplicationVersion:        $currentVersionText"
    Write-Host "New ApplicationVersion:            $newVersionFormatted"
    Write-Host "New ApplicationDisplayVersion:     $newDisplayVersion"

    # Only update ApplicationVersion in the csproj, leave ApplicationDisplayVersion unchanged
    $versionNode.ApplicationVersion = $newVersionFormatted

    # Resolve to absolute path — XmlDocument.Save() uses .NET's Environment.CurrentDirectory,
    # not PowerShell's $PWD, so relative paths save to the wrong location.
    $absolutePath = (Resolve-Path $ProjectPath).Path
    $xml.Save($absolutePath)

    Write-Host "Version successfully updated: $newDisplayVersion"
    exit 0
}
catch {
    Write-Error "Error incrementing version: $($_.Exception.Message)"
    exit 1
}
