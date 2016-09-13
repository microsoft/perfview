#Making the *.SupportFiles.nupkg NuGet Packages.

The build of PerfView depends on two packages of support files.  

 * Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles.1.0.0
 * PerfView.SupportFiles.1.0.0


These two Nuget packages contain binaries (DLLs and tools) that are assumed as 
part of the underlying platform, so are not created as part of the build.  These
include 

 * The native code msdia* library that allows you to decode PDB files
 * The native KernelTraceControl* library that allows for merging of ETL files
 * Managed COM interop assemblies (generated with the TLBEXP tool on native type libraries)


These Nuget packages are available on https://www.nuget.org/ so the build should 'just work'
but it is certainly possible that at some point we will want to update these binaries. 
and this document indicats how to do this.  

There are files in the 'NugetSupportFiles' directory for regenerating these two Nuget packages.
We go through the procedure for PerfView.SupportFiles.1.0.0 but the same technique works for
Microsoft.Diagnostics.Tracing.TraceEvent.SupportFiles.1.0.0.


## Step 1 populate binaries you are not updating


It is likely that you only need to update a subset of all the DLLs in the package.  Thus you
need to start with an existing set.  You can do this by running the PerfView.SupportFiles.Populate.bat
script.  This will copy all the files out of the 'packages' directory to form a baseline.  
This batch script also has a list of the files it is going to copy so that even if you don't
have the existing nuget package, you can populate the new package 'by hand' from 'raw' files.

## Step 2 Update the files in the baseline Image (PerfView.SupportFiles)

This is dependent on exactly what you want to do. However something you should ALWAYS do
is to update the version number in the PerfView.SupportFiles.nuspec file.  

## Step 3 Generate the nuget package

There is a script called PerfView.SupportFiles.MakeNuget.bat which does this.  It is a one line
script, and generates a new *.nupkg in the current directory.  

## Step 4 Upload to https://www.nuget.org/

Simply go to https://www.nuget.org/ log in, and follow the instructions on the 'Upload Package' link.
