@echo off
echo Building ImpactFrame mod for Bonelab...

dotnet build -c Release

if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Build successful!
echo Output: bin\Release\netstandard2.0\ImpactFrame.dll
echo.
echo To install, copy ImpactFrame.dll to:
echo %USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\Mods\
echo.
pause