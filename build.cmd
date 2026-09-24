@echo off
setlocal

set "RepoRoot=%~dp0"
set "Configuration=Debug"
set "ConfigurationSpecified="
set "MSBuildArguments="

:ParseArguments
if "%~1"=="" goto ArgumentsParsed

if /i "%~1"=="/Configuration" goto ReadConfiguration
if /i "%~1"=="-Configuration" goto ReadConfiguration

set "BuildArgument=%~1"
if /i "%BuildArgument:~0,16%"=="/p:Configuration" goto ConfigurationPropertyError
if /i "%BuildArgument:~0,16%"=="-p:Configuration" goto ConfigurationPropertyError
if /i "%BuildArgument:~0,23%"=="/property:Configuration" goto ConfigurationPropertyError
if /i "%BuildArgument:~0,23%"=="-property:Configuration" goto ConfigurationPropertyError

set "MSBuildArguments=%MSBuildArguments% %1"
shift
goto ParseArguments

:ReadConfiguration
if defined ConfigurationSpecified goto DuplicateConfigurationError
set "ConfigurationSpecified=1"
set "ConfigurationOption=%~1"
shift
if "%~1"=="" goto MissingConfigurationError
if /i "%~1"=="Debug" goto DebugConfiguration
if /i "%~1"=="Release" goto ReleaseConfiguration
echo error: Unsupported configuration '%~1'. Expected Debug or Release. 1>&2
exit /b 1

:DebugConfiguration
set "Configuration=Debug"
goto ConfigurationRead

:ReleaseConfiguration
set "Configuration=Release"
goto ConfigurationRead

:ConfigurationRead
shift
goto ParseArguments

:ArgumentsParsed
pushd "%RepoRoot%"
if errorlevel 1 (
    echo error: Unable to access the repository root '%RepoRoot%'. 1>&2
    exit /b 1
)

set "RequiredSdkVersion="
for /f "usebackq tokens=1,2 delims=:, " %%A in ("%RepoRoot%global.json") do if /i "%%~A"=="version" set "RequiredSdkVersion=%%~B"
if not defined RequiredSdkVersion (
    echo error: Unable to read sdk.version from '%RepoRoot%global.json'. 1>&2
    popd
    exit /b 1
)

for /f "tokens=1 delims=." %%M in ("%RequiredSdkVersion%") do set "SdkMajorVersion=%%M"

where dotnet.exe >nul 2>&1
if errorlevel 1 (
    echo error: Unable to find dotnet.exe. Install the .NET SDK selected by global.json and ensure dotnet.exe is on PATH. 1>&2
    echo Install the required SDK with: 1>&2
    echo   winget install -e --id Microsoft.DotNet.SDK.%SdkMajorVersion% -v "%RequiredSdkVersion%" 1>&2
    popd
    exit /b 1
)

dotnet --version
if not "%ERRORLEVEL%"=="0" (
    echo error: No installed .NET SDK satisfies global.json. Install the requested SDK or a permitted later patch and try again. 1>&2
    echo Install the required SDK with: 1>&2
    echo   winget install -e --id Microsoft.DotNet.SDK.%SdkMajorVersion% -v "%RequiredSdkVersion%" 1>&2
    popd
    exit /b 1
)

set "VSWhere=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWhere%" (
    echo error: Unable to find vswhere.exe. Install Visual Studio 2026 with the components listed in the repository's .vsconfig file. 1>&2
    popd
    exit /b 1
)

set "VisualStudioPath="
for /f "usebackq delims=" %%I in (`^""%VSWhere%" -latest -prerelease -version "[18.0,19.0)" -products * -requires Microsoft.Component.MSBuild Microsoft.Net.Component.4.6.2.SDK Microsoft.Net.Component.4.6.2.TargetingPack Microsoft.VisualStudio.Component.VC.Tools.x86.x64 Microsoft.VisualStudio.Component.VC.Runtimes.x86.x64.Spectre Microsoft.VisualStudio.Component.Windows10SDK.* -property installationPath^"`) do set "VisualStudioPath=%%I"

if not defined VisualStudioPath (
    for /f "usebackq delims=" %%I in (`^""%VSWhere%" -latest -prerelease -version "[18.0,19.0)" -products * -requires Microsoft.Component.MSBuild Microsoft.Net.Component.4.6.2.SDK Microsoft.Net.Component.4.6.2.TargetingPack Microsoft.VisualStudio.Component.VC.Tools.x86.x64 Microsoft.VisualStudio.Component.VC.Runtimes.x86.x64.Spectre Microsoft.VisualStudio.Component.Windows11SDK.* -property installationPath^"`) do set "VisualStudioPath=%%I"
)

if not defined VisualStudioPath (
    echo error: Unable to find Visual Studio 2026 with MSBuild, .NET Framework 4.6.2 tools, C++ build tools, Spectre libraries, and a Windows SDK. Install the components listed in the repository's .vsconfig file. 1>&2
    popd
    exit /b 1
)

set "MSBuild=%VisualStudioPath%\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%MSBuild%" (
    echo error: Visual Studio 2026 was found at '%VisualStudioPath%', but MSBuild.exe is missing. 1>&2
    popd
    exit /b 1
)

echo Using MSBuild from %MSBuild%
"%MSBuild%" "%RepoRoot%PerfView.sln" /restore /m /p:Configuration=%Configuration% %MSBuildArguments%
set "BuildExitCode=%ERRORLEVEL%"

if "%BuildExitCode%"=="0" (
    echo.
    echo *************************************************************
    echo                The build was successful!
    echo The output is in src\PerfView\bin\%Configuration%\net462\PerfView.exe
    echo This is the only file needed to deploy the program.
    echo *************************************************************
)

popd
exit /b %BuildExitCode%

:DuplicateConfigurationError
echo error: Configuration was specified more than once. Use one '/Configuration ^<Debug^|Release^>' or '-Configuration ^<Debug^|Release^>' option. 1>&2
exit /b 1

:MissingConfigurationError
echo error: The '%ConfigurationOption%' option requires a value of Debug or Release. 1>&2
exit /b 1

:ConfigurationPropertyError
echo error: Pass the build configuration with '/Configuration ^<Debug^|Release^>' or '-Configuration ^<Debug^|Release^>', not '%BuildArgument%'. 1>&2
exit /b 1
