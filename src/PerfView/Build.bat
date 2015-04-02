@REM Created by the ScriptLib Utility
@echo off
setlocal
pushd %~dp0
set MSBuildDir=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
set PATH=%MSBuildDir%;%PATH%
if /I "%1" == "/Release" (
    MSBuild.exe /p:Configuration=Release "PerfView.sln" %2 %3 %4 %5 %6 %7 %8 %9
    echo.
    echo.Created an optimized version of the project.
) else (
    MSBuild.exe /p:Configuration=Debug "PerfView.sln" %*
    echo.
    echo.A debug version of the project was generated
    echo.Use 'build /Release' to create an optimized version of the project.
)
