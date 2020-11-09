REM copies from an existing build to a nuget package creation area (so that *.MakeNuget.bat works)
REM
REM *** This is mostly a template for doing the copy.      ****  
REM *** Most likely you want this to be the current version ****
REM *** PLEASE MODIFY THE VERSION NUMBER TO BE CURRENT!    ****
@if "%1" == "" (
    echo Error Must specify the last component of the version number of MicrosoftDiagnosticsTracingTraceEventSupportFilesVersion from Directory.Build.props
	exit /b 1
)
xcopy /s /Y %HOMEDRIVE%%HOMEPATH%\.nuget\packages\Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles\1.0.%1\*.dll Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles
xcopy /s /Y %HOMEDRIVE%%HOMEPATH%\.nuget\packages\Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles\1.0.%1\*.pdb Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles
@if NOT "%ERRORLEVEL%" == "0" (
    echo *****  Bad Version Number %1.  ******
	exit /b 1
)

REM Overwrite OSExtensions.dll with the latest built versions.  However you want the signed versions of these 
@REM Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles\lib\net45
@REM Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles\lib\netstandard1.6

@REM These are the binary files we need from somewhere to for the support package
@REM lib\native\amd64\KernelTraceControl.dll
@REM lib\native\amd64\msdia140.dll
@REM lib\native\arm\KernelTraceControl.dll
@REM lib\native\x86\KernelTraceControl.dll
@REM lib\native\x86\KernelTraceControl.Win61.dll
@REM lib\native\x86\msdia140.dll
@REM lib\net45\Dia2Lib.dll
@REM lib\net45\OSExtensions.dll
@REM lib\net45\OSExtensions.pdb
@REM lib\net45\TraceReloggerLib.dll
@REM lib\netstandard1.6\Dia2Lib.dll
@REM lib\netstandard1.6\OSExtensions.dll
@REM lib\netstandard1.6\OSExtensions.pdb
@REM lib\netstandard1.6\TraceReloggerLib.dll
