@echo off
REM Batch script to publish a single-file executable
REM Usage: publish-release.bat

echo Building release distribution as single EXE file...

REM Clean previous publish
if exist "publish\Release" (
    echo Cleaning previous publish output...
    rmdir /s /q "publish\Release"
)

REM Publish as single file
echo Publishing application...
dotnet publish -c Release -r win-x64 ^
    -p:PublishSingleFile=true ^
    -p:SelfContained=true ^
    -p:PublishReadyToRun=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:RuntimeIdentifier=win-x64 ^
    --output "publish\Release"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    
    REM Remove config files - users should provide their own
    if exist "publish\Release\config.ini" (
        del /q "publish\Release\config.ini" 2>nul
        echo Removed config.ini (users will provide their own)
    )
    if exist "publish\Release\private_config.ini" (
        del /q "publish\Release\private_config.ini" 2>nul
        echo Removed private_config.ini (users will provide their own)
    )
    
    echo.
    echo Output location: publish\Release\StarResonanceDpsAnalysis.exe
    echo.
    echo Note: Config files (config.ini, private_config.ini) should be placed alongside the exe for users to configure.
) else (
    echo.
    echo Build failed!
    exit /b %ERRORLEVEL%
)

