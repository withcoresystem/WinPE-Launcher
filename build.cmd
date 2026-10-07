@echo off
setlocal enabledelayedexpansion
set "ROOT=%~dp0"
set "MSBUILD="

for /f "usebackq delims=" %%i in (`where msbuild 2^>nul`) do if not defined MSBUILD set "MSBUILD=%%i"

if not defined MSBUILD if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" (
  for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do if not defined MSBUILD set "MSBUILD=%%i"
)

if not defined MSBUILD (
  echo [ERROR] MSBuild not found.
  exit /b 1
)

echo Using: %MSBUILD%
"%MSBUILD%" "%ROOT%Launcher.csproj" /t:Rebuild /p:Configuration=Release /v:m /nologo
if errorlevel 1 (
  echo [ERROR] Build failed.
  exit /b 1
)

echo.
echo Output:
dir /b "%ROOT%bin\Release"
