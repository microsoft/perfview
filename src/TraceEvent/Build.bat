REM  Copyright (c) Microsoft Corporation.  All rights reserved
@echo off
setlocal
pushd %~dp0
set MSBuildDir=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
if NOT EXIST %MSBuildDir%\MSBuild.exe (
    set MSBuildDir=%WINDIR%\Microsoft.NET\Framework\v2.0.50727\\..\v3.5
)
if NOT EXIST %MSBuildDir%\MSBuild.exe (
    echo.Error Cannot find a V3.5 Version of MSBuild.  Please upgrade runtime to at least V3.5 
    exit /b 1
)
set PATH=%MSBuildDir%;%PATH%
if /I "%1" == "/Release" (
    MSBuild.exe /p:Configuration=Release TraceEvent.VS2008.sln
    echo.
    echo.Created an optimized version of the project.
) else (
    MSBuild.exe /p:Configuration=Debug TraceEvent.VS2008.sln
    echo.
    echo.A debug version of the project was generated
    echo.Use 'build /Release' to create an optimized version of the project.
)
