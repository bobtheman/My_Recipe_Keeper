param(
    [string]$ProjectDir,
    [string]$ApplicationDisplayVersion,
    [string]$ApplicationVersion
)

# Combine display version and internal version: 1.0 + 7 -> 1_0_7
$VersionForName = $ApplicationDisplayVersion -replace '\.', '_'
$VersionForName = "$VersionForName`_$ApplicationVersion"

Write-Host "================================================" -ForegroundColor Yellow
Write-Host "RenameAPK/AAB Script" -ForegroundColor Yellow
Write-Host "================================================" -ForegroundColor Yellow
Write-Host "Project Directory: $ProjectDir"
Write-Host "ApplicationDisplayVersion: $ApplicationDisplayVersion"
Write-Host "ApplicationVersion: $ApplicationVersion"
Write-Host "Version for filename: $VersionForName"
Write-Host "================================================" -ForegroundColor Yellow

# Search both directories where MAUI places the signed APK/AAB
$SearchDirs = @(
    (Join-Path $ProjectDir "bin\Release\net10.0-android\publish"),
    (Join-Path $ProjectDir "bin\Release\net10.0-android")
)

$RenamedCount = 0

foreach ($Dir in $SearchDirs) {
    if (-not (Test-Path $Dir)) {
        Write-Host "Directory not found, skipping: $Dir" -ForegroundColor Gray
        continue
    }

    # Rename APK files
    $SignedAPKs = Get-ChildItem -Path $Dir -Filter "*-Signed.apk" -ErrorAction SilentlyContinue
    foreach ($SignedAPK in $SignedAPKs) {
        $TargetAPKName = "My_Recipe_Keeper_$VersionForName.apk"
        $SourcePath = $SignedAPK.FullName
        $TargetPath = Join-Path $Dir $TargetAPKName

        Write-Host "Found APK: $SourcePath" -ForegroundColor Green
        Write-Host "Renaming to: $TargetPath" -ForegroundColor Green

        try {
            if (Test-Path $TargetPath) {
                Remove-Item $TargetPath -Force
            }
            Copy-Item -Path $SourcePath -Destination $TargetPath -Force
            Remove-Item -Path $SourcePath -Force
            Write-Host "Renamed successfully!" -ForegroundColor Green
            $RenamedCount++
        }
        catch {
            Write-Host "ERROR: Failed to rename: $_" -ForegroundColor Red
        }
    }

    # Rename AAB files (Android App Bundle)
    $SignedAABs = Get-ChildItem -Path $Dir -Filter "*-Signed.aab" -ErrorAction SilentlyContinue
    foreach ($SignedAAB in $SignedAABs) {
        $TargetAABName = "My_Recipe_Keeper_$VersionForName.aab"
        $SourcePath = $SignedAAB.FullName
        $TargetPath = Join-Path $Dir $TargetAABName

        Write-Host "Found AAB: $SourcePath" -ForegroundColor Green
        Write-Host "Renaming to: $TargetPath" -ForegroundColor Green

        try {
            if (Test-Path $TargetPath) {
                Remove-Item $TargetPath -Force
            }
            Copy-Item -Path $SourcePath -Destination $TargetPath -Force
            Remove-Item -Path $SourcePath -Force
            Write-Host "Renamed successfully!" -ForegroundColor Green
            $RenamedCount++
        }
        catch {
            Write-Host "ERROR: Failed to rename: $_" -ForegroundColor Red
        }
    }
}

if ($RenamedCount -eq 0) {
    Write-Host "WARNING: No signed APK/AAB files found to rename." -ForegroundColor Yellow
    Write-Host "Searched directories:" -ForegroundColor Yellow
    foreach ($Dir in $SearchDirs) {
        Write-Host "  - $Dir" -ForegroundColor Yellow
        if (Test-Path $Dir) {
            $files = Get-ChildItem -Path $Dir -Filter "*.apk" -ErrorAction SilentlyContinue
            $files += Get-ChildItem -Path $Dir -Filter "*.aab" -ErrorAction SilentlyContinue
            foreach ($f in $files) { Write-Host "    Found: $($f.Name)" -ForegroundColor Gray }
        }
    }
}

Write-Host "================================================" -ForegroundColor Green
Write-Host "Renamed $RenamedCount file(s) with version: $VersionForName" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green
exit 0
