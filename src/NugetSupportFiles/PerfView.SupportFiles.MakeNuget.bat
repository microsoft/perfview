@if NOT EXIST "PerfView.SupportFiles\lib\native\x86\sd.exe"
   echo Error the package does not seem to be populated with binaries.  
   echo Run PerfView.SupportFiles.Populate and update before running. 
   exit /b 1
)
nuget pack PerfView.SupportFiles\PerfView.SupportFiles.nuspec
