# PowerShell script to publish a single-file executable
# Usage: .\publish-release.ps1

Write-Host "Building release distribution as single EXE file..." -ForegroundColor Green

# Clean previous publish
if (Test-Path "publish\Release") {
    Write-Host "Cleaning previous publish output..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force "publish\Release"
}

# Publish as single file
Write-Host "Publishing application..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:RuntimeIdentifier=win-x64 `
    --output "publish\Release"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild successful!" -ForegroundColor Green
    
    # Remove config files - users should provide their own
    $configFiles = @(
        "publish\Release\config.ini",
        "publish\Release\private_config.ini"
    )
    foreach ($file in $configFiles) {
        if (Test-Path $file) {
            Remove-Item $file -Force -ErrorAction SilentlyContinue
            $fileName = Split-Path $file -Leaf
            Write-Host "Removed $fileName (users will provide their own)" -ForegroundColor Gray
        }
    }
    
    Write-Host "`nOutput location: publish\Release\StarResonanceDpsAnalysis.exe" -ForegroundColor Cyan
    Write-Host "`nNote: Config files (config.ini, private_config.ini) should be placed alongside the exe for users to configure." -ForegroundColor Yellow
    
    # Get file size
    $exePath = "publish\Release\StarResonanceDpsAnalysis.exe"
    if (Test-Path $exePath) {
        $fileSize = (Get-Item $exePath).Length / 1MB
        Write-Host "`nExecutable size: $([math]::Round($fileSize, 2)) MB" -ForegroundColor Cyan
    }
} else {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

