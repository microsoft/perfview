REM copies from an existing build to a nuget package creation area (so that *.MakeNuget.bat works)
xcopy /s ..\..\packages\Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles.1.0.1\*.dll Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles

@REM These are the binary files we need from somewhere to for the support package
@REM lib\native\amd64\KernelTraceControl.dll
@REM lib\native\amd64\msdia140.dll
@REM lib\native\arm\KernelTraceControl.dll
@REM lib\native\x86\KernelTraceControl.dll
@REM lib\native\x86\KernelTraceControl.Win61.dll
@REM lib\native\x86\msdia140.dll
@REM lib\net40\Interop.Dia2Lib.dll
@REM lib\net40\OSExtensions.dll
@REM lib\net40\TraceReloggerLib.dll
