REM copies from an existing build to a nuget package creation area (so that *.MakeNuget.bat works)
xcopy /s /y ..\PerfView\packages\PerfView.SupportFiles.1.0.0\*.dll PerfView.SupportFiles
xcopy /s /y ..\PerfView\packages\PerfView.SupportFiles.1.0.0\*.exe PerfView.SupportFiles

@REM These are the binary files we need from somewhere to for the support package
@REM lib\native\x86\DiagnosticsHub.Packaging.dll
@REM lib\native\x86\DiagnosticsHub.Packaging.Interop.dll
@REM lib\native\x86\sd.exe
@REM lib\net40\DiagnosticsHub.Packaging.Interop.dll
@REM lib\net40\Microsoft.Diagnostics.Tracing.EventSource.dll
@REM lib\net40\Microsoft.DiagnosticsHub.Packaging.InteropEx.dll
@REM tools\MC.Exe
