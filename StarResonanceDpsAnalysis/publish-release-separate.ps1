# PowerShell script to publish a single-file executable with native DLLs kept separate
# This matches the original DPS2.0.5EN packaging style:
# - Single-file EXE (181MB) with all .NET assemblies embedded
# - Native DLLs kept as separate files (WPF rendering, DirectX, Zstd, etc.)
# Usage: .\publish-release-separate.ps1

Write-Host "Building single-file distribution with native DLLs separate (like DPS2.0.5EN)..." -ForegroundColor Green

# Clean previous publish
if (Test-Path "publish\Release-Separate") {
    Write-Host "Cleaning previous publish output..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force "publish\Release-Separate"
}

# Publish as single-file BUT with native libraries as separate files (not extracted)
# This matches the original: huge EXE + only native DLLs alongside
Write-Host "Publishing application..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=false `
    -p:EnableCompressionInSingleFile=true `
    -p:RuntimeIdentifier=win-x64 `
    --output "publish\Release-Separate"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild successful!" -ForegroundColor Green
    
    # Copy libzstd.dll from x64 subdirectory to root (we only need x64 version)
    $x64Libzstd = "publish\Release-Separate\x64\libzstd.dll"
    $rootLibzstd = "publish\Release-Separate\libzstd.dll"
    if (Test-Path $x64Libzstd) {
        Copy-Item $x64Libzstd $rootLibzstd -Force
        Write-Host "Copied libzstd.dll from x64\ to root folder" -ForegroundColor Gray
    }
    
    # Remove x64 and x86 subdirectories - we only want native DLLs in root folder
    $x64Dir = "publish\Release-Separate\x64"
    $x86Dir = "publish\Release-Separate\x86"
    if (Test-Path $x64Dir) {
        Remove-Item $x64Dir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Removed x64\ directory" -ForegroundColor Gray
    }
    if (Test-Path $x86Dir) {
        Remove-Item $x86Dir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Removed x86\ directory" -ForegroundColor Gray
    }
    
    # Remove Core subdirectory if it exists (only contains runtime config)
    $coreDir = "publish\Release-Separate\Core"
    if (Test-Path $coreDir) {
        Remove-Item $coreDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Removed Core\ directory" -ForegroundColor Gray
    }
    
    # Remove config files - users should provide their own
    $configFiles = @(
        "publish\Release-Separate\config.ini",
        "publish\Release-Separate\private_config.ini"
    )
    foreach ($file in $configFiles) {
        if (Test-Path $file) {
            Remove-Item $file -Force -ErrorAction SilentlyContinue
            $fileName = Split-Path $file -Leaf
            Write-Host "Removed $fileName (users will provide their own)" -ForegroundColor Gray
        }
    }
    
    Write-Host "`nOutput location: publish\Release-Separate\" -ForegroundColor Cyan
    Write-Host "`nNote: Single-file EXE with native DLLs kept separate (matches DPS2.0.5EN style)" -ForegroundColor Yellow
    Write-Host "The EXE will be large (~110MB) with only native libraries as separate files." -ForegroundColor Yellow
    
    # Get folder size
    $folderPath = "publish\Release-Separate"
    if (Test-Path $folderPath) {
        $folderSize = (Get-ChildItem -Path $folderPath -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
        Write-Host "`nTotal folder size: $([math]::Round($folderSize, 2)) MB" -ForegroundColor Cyan
        
        # Count files (only in root, not subdirectories)
        $fileCount = (Get-ChildItem -Path $folderPath -File).Count
        Write-Host "Number of files: $fileCount" -ForegroundColor Cyan
        
        # Verify libzstd.dll is present in root
        if (Test-Path "$folderPath\libzstd.dll") {
            Write-Host "libzstd.dll: Present in root folder" -ForegroundColor Green
        } else {
            Write-Host "libzstd.dll: MISSING in root folder" -ForegroundColor Red
        }
    }
} else {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

