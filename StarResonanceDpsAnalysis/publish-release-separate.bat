@echo off
REM Batch script to publish a single-file executable with native DLLs kept separate
REM This matches the original DPS2.0.5EN packaging style:
REM - Single-file EXE (181MB) with all .NET assemblies embedded
REM - Native DLLs kept as separate files (WPF rendering, DirectX, Zstd, etc.)
REM Usage: publish-release-separate.bat

echo Building single-file distribution with native DLLs separate (like DPS2.0.5EN)...

REM Clean previous publish
if exist "publish\Release-Separate" (
    echo Cleaning previous publish output...
    rmdir /s /q "publish\Release-Separate"
)

REM Publish as single-file BUT with native libraries as separate files (not extracted)
REM This matches the original: huge EXE + only native DLLs alongside
echo Publishing application...
dotnet publish -c Release -r win-x64 ^
    -p:PublishSingleFile=true ^
    -p:SelfContained=true ^
    -p:PublishReadyToRun=true ^
    -p:IncludeNativeLibrariesForSelfExtract=false ^
    -p:EnableCompressionInSingleFile=true ^
    -p:RuntimeIdentifier=win-x64 ^
    --output "publish\Release-Separate"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    
    REM Copy libzstd.dll from x64 subdirectory to root (we only need x64 version)
    if exist "publish\Release-Separate\x64\libzstd.dll" (
        copy /Y "publish\Release-Separate\x64\libzstd.dll" "publish\Release-Separate\" >nul
        echo Copied libzstd.dll from x64\ to root folder
    )
    
    REM Remove x64 and x86 subdirectories - we only want native DLLs in root folder
    if exist "publish\Release-Separate\x64" (
        rmdir /s /q "publish\Release-Separate\x64" 2>nul
        echo Removed x64\ directory
    )
    if exist "publish\Release-Separate\x86" (
        rmdir /s /q "publish\Release-Separate\x86" 2>nul
        echo Removed x86\ directory
    )
    
    REM Remove Core subdirectory if it exists (only contains runtime config)
    if exist "publish\Release-Separate\Core" (
        rmdir /s /q "publish\Release-Separate\Core" 2>nul
        echo Removed Core\ directory
    )
    
    REM Remove config files - users should provide their own
    if exist "publish\Release-Separate\config.ini" (
        del /q "publish\Release-Separate\config.ini" 2>nul
        echo Removed config.ini (users will provide their own)
    )
    if exist "publish\Release-Separate\private_config.ini" (
        del /q "publish\Release-Separate\private_config.ini" 2>nul
        echo Removed private_config.ini (users will provide their own)
    )
    
    echo.
    echo Output location: publish\Release-Separate\
    echo.
    echo Note: Single-file EXE with native DLLs kept separate (matches DPS2.0.5EN style)
    echo The EXE will be large (~110MB) with only native libraries as separate files.
    
    REM Verify libzstd.dll is present in root
    if exist "publish\Release-Separate\libzstd.dll" (
        echo libzstd.dll: Present in root folder
    ) else (
        echo libzstd.dll: MISSING in root folder
    )
) else (
    echo.
    echo Build failed!
    exit /b %ERRORLEVEL%
)

