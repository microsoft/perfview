@if NOT EXIST "Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles\lib\native\x86\msdia140.dll" (
   echo Error the package does not seem to be populated with binaries.  
   echo Run Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles.Populate and update before running. 
   exit /b 1
)
nuget pack Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles\Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles.nuspec
