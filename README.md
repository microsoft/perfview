# PerfView Overview
PerfView is a free performance-analysis tool that helps isolate CPU and memory-related performance issues.  It is a Windows tool, but it also has some support for analyzing data collected on Linux machines.  It works for a wide variety of scenarios, but has a number of special features for investigating performance issues in code written for the .NET runtime.  

If you are unfamiliar with PerfView, there are [PerfView video tutorials](http://channel9.msdn.com/Series/PerfView-Tutorial). 
Also, [Vance Morrison's blog](https://docs.microsoft.com/en-us/archive/blogs/vancem/) gives overview and getting 
started information. 

### Getting PerfView 
Please see the [PerfView Download Page](documentation/Downloading.md) for the link and instructions for downloading the 
current version of PerfView.  

PerfView requires .NET Framework 4.7.2 or later, which is widely available for all supported versions of Windows.

### Are you here about the TraceEvent Library?

PerfView is built on a library called Microsoft.Diagnostics.Tracing.TraceEvent, that knows how to both collect and parse Event Tracing for Windows (ETW) and EventPipe (.NET Core trace) data. Thus if there is any information that PerfView collects and processes that you would like to manipulate yourself programmatically, you would probably be interested in the [TraceEvent Library Documentation](documentation/TraceEvent/TraceEventLibrary.md)

### Not Sure if you should use PerfView or TraceEvent?

See the [scenarios](documentation/Scenarios.md) document to determine which is the best choice for what you're trying to do.

### Learning about PerfView 

The PerfView User's Guide is part of the application itself. In addition, you can click the
[Users Guide link](http://htmlpreview.github.io/?https://github.com/Microsoft/perfview/blob/main/src/PerfView/SupportFiles/UsersGuide.htm) 
to see the [GitHub HTML Source File](src/PerfView/SupportFiles/UsersGuide.htm) rendered in your browser.  You can also simply
download PerfView using the instructions above and select the Help -> User's Guide menu item. 

### Asking Questions / Reporting Bugs 

When you have question about PerfView, your first reaction should be to search the Users Guide (Help -> User's Guide) and 
see if you can find the answer already.   If that does not work you can ask a question by creating a [new PerfView Issue](https://github.com/Microsoft/perfview/issues/new).
State your question succinctly in the title, and if necessary give details in the body of the issue, there is an issue tag
called 'question' that you should use as well that marks your issue as a question rather than some bug report.
If the question is specific to a particular trace (*.ETL.ZIP file) you can drag that file onto the issue and it will be downloaded.
This allows those watching for issues to reproduce your environment and give much more detailed and useful answers.

Note that once you have your question answered, if the issue is likely to be common, you should strongly consider updating the
documentation to include the information.  The documentation is pretty much just
one file https://github.com/Microsoft/perfview/blob/main/src/PerfView/SupportFiles/UsersGuide.htm.
You will need to clone the repository and create a pull request (see [OpenSourceGitWorkflow](https://github.com/Microsoft/perfview/blob/main/documentation/OpenSourceGitWorkflow.md)
for instructions for setting up and creating a pull request.  

Reporting bugs works pretty much the same way as asking a question.  It is very likely that you will want to include the *.ETL.ZIP
file needed to reproduce the problem as well as any steps and the resulting undesirable behavior.

# Building PerfView Yourself

If you just want to do a performance investigation, you don't need to build PerfView yourself.
Just use the one from the [PerfView Download Page](documentation/Downloading.md).
However if you want new features or just want to contribute to PerfView to make it better
(see [issues](https://github.com/Microsoft/perfview/issues) for things people want)
you can do that by following the rest of these instructions.

### Tools Needed to Build PerfView

The only tool you need to build PerfView is Visual Studio 2022.  The [Visual Studio 2022 Community Edition](https://www.visualstudio.com/vs/community/) 
can be downloaded *for free* and has everything you need to fetch PerfView from GitHub, build and test it. We expect you 
to download Visual Studio 2022 Community Edition if you don't already have Visual Studio 2022.

In your installation of Visual Studio, you need to ensure you have the following workloads and components installed:

  * **.NET desktop development** workload with all default components.
  * **Desktop development with C++** workload with all default components plus the latest Windows 10 SDK.
    * The Windows 10 SDK is not enabled by default in this workload, so you will need to check the box for it to be installed.
  * **MSVC v143 - VS 2022 C++ x64/x86 Spectre-mitigated libs (Latest)** component.
    * This can be found under the 'Individual Components' tab.

A `.vsconfig` file is included in the root of the repository that can be used to install the necessary components. When 
opening the solution in Visual Studio, it will prompt you to install any components that it thinks are missing from your 
installation.  Alternatively, you can [import the `.vsconfig` in the Visual Studio Installer](https://learn.microsoft.com/en-us/visualstudio/install/import-export-installation-configurations?view=vs-2022#import-a-configuration).

If you get any errors compiling the ETWClrCompiler projects, it is likely because you either don't have the Windows 10 SDK 
installed, or you don't have the spectre-mitigated libs installed. Please refer to the [troubleshooting section](#information-for-build-troubleshooting) for more information.


### Cloning the PerfView GitHub Repository. 

The first step in getting started with the PerfView source code is to clone the PerfView GitHub repository.
If you are already familiar with how GIT, GitHub, and Visual Studio 2022 GIT support works, then you can skip this section.
However, if not, the [Setting up a Local GitHub repository with Visual Studio 2022](documentation/SettingUpRepoInVS.md) document
will lead you through the basics of doing this. All it assumes is that you have Visual Studio 2022 installed.

### How to Build and Debug PerfView 

PerfView is developed in Visual Studio 2022 using features through C# 7.3.

  * The solution file is PerfView.sln.  Opening this file in Visual Studio (or double clicking on it in 
  the Windows Explorer) and selecting Build -> Build Solution, will build it. You can also build the 
  non-debug version from the command line using msbuild or the build.cmd file at the base of the repository.
  The build follows standard Visual Studio conventions, and the resulting PerfView.exe file ends up in
  src/PerfView/bin/*BuildType*/PerfView.exe. You need only deploy this one EXE to use it.  

  * The solution consists of several projects, representing support DLLs and the main EXE. To run PerfView in the 
  debugger **you need to make sure that the 'Startup Project' is set to the 'PerfView' project** so that it launches 
  the main EXE.   If the PerfView project in the Solution Explorer (on the right) is not bold, right click on the PerfView project 
  and select 'Set as Startup Project'. After doing this 'Start Debugging' (F5) should work.

### Deploying your new version of Perfview

You will want to deploy the 'Release' rather than the 'Debug' version of PerfView.  Thus, first set your build configuration
to 'Release' (Text window in the top toolbar, or right click on the .SLN file -> Configuration Manager -> Active Solution Configuration).
Next build (Build -> Build Solution (Ctrl-Shift-B)).   The result will be that in the src\perfView\bin\net462\Release directory there will be
among other things a PerfView.exe.   This one file is all you need to deploy.   Simply copy it to where you wish to deploy the app.  

### Information for build troubleshooting.  

  * One of the unusual things about PerfView is that it incorporates its support DLLs into the EXE itself, and these get 
  unpacked on first launch.  This means that there are tricky dependencies in the build that are not typical.    You will 
  see errors that certain DLLs can't be found if there were build problems earlier in the build.   Typically you can fix 
  this simply by doing a normal (non-clean) build, since the missing file will be present from the last compilation.
  If this does not fix things, see if the DLL being looked for actually exists (if it does, then rebuilding should fix it).
  It can make sense to go down the projects one by one and build them individually to see which one fails 'first'.
  
  * Another unusual thing about PerfView is that it includes an extension mechanism complete with samples.
  This extensions mechanism is the 'Global' project (called that because it is the Global Extension whose commands don't have an
  explicit 'scope') and needs to refer to PerfView to resolve some of its references.   Thus you will get many 'not found'
  issues in the 'Global' project.  These can be ignored until you get every other part of the build working.

  * One of the invariants of the repo is that if you are running Visual Studio 2022 and you simply sync and build the
  PerfView.sln file, it is supposed to 'just work'.   If that does not happen, and the advice above does not help, then
  we need to either fix the repo or update the advice above. Thus it is reasonable to open a GitHub issue. If you
  do this, the goal is to fix the problem, which means you have to put enough information into the issue to do that.
  This includes exactly what you tried, and what the error messages were.
  
  * You can also build PerfView from the command line (but you still need Visual Studio 2022 installed).   It is a two step process.
  First you must restore all the needed nuget packages, then you do the build itself. To do this:
    1. Open a developer command prompt.  You can do this by hitting the windows key (by the space bar) and type
       'Developer command prompt'.  You should see a entry for this that you can select (if Visual Studio 2022 is installed).
    2. Change directory to the base of your PerfView source tree (where PerfView.sln lives). 
    3. Restore the nuget packages by typing the command 'msbuild /t:restore'
    4. Build perfView by typing the command 'msbuild'
  
  * If you get an error "MSB8036: The Windows SDK version 10.0.17763.0 was not found",  Or you get a 'assert.h' not found error, or 
  frankly any error associated with building the ETWClrProfiler dlls, you should make sure that you have the Windows 10 SDK installed.  Unfortunately this library tends not to be 
  installed with Visual Studio anymore unless you ask for it explicitly.   To fix it launch the Visual Studio Installer, modify the installation, and then look under the C++ Desktop Development and check that the Windows SDK 10.0.17763.0 option is selected.  If not, select it and continue.  Then try building PerfView again.

  * If you get an error "MSB8040: Spectre-mitigated libraries are required for this project",  modify your Visual Studio 
    installation to ensure that you have the 'MSVC v143 - VS 2022 C++ x64/x86 Spectre-mitigated libs (Latest)' component installed. 

  
### Running Tests

PerfView has a number of *.Test projects that have automated tests.  They can be run in Visual Studio by selecting the
Test -> Run -> All Tests menu item.    For the most thorough results (and certainly if you intend to submit changes) you 
need to run these tests with a Debug build of the product (see the text window in the top toolbar, it says 'Debug' or 'Release').
If tests fail you can right click on the failed test and select the 'Debug' context menu item to run the test under 
the debugger to figure out what went wrong.  

### Check in testing and code coverage statistica

This repository uses Azure DevOps to automatically build and test pull requests, which allows
the community to easily view build results. The build and status reflected here is the Azure DevOps build status of the **main** branch.

[![Build Status](https://dev.azure.com/ms/perfview/_apis/build/status%2FCI?branchName=main)](https://dev.azure.com/ms/perfview/_build/latest?definitionId=332&branchName=main)

> :warning: Builds produced by Azure DevOps CI are not considered official builds of PerfView, and are not signed or otherwise
> validated for safety or security in any way. This build integration is provided as a convenience for community
> participants, but is not endorsed by Microsoft nor is it considered an official release channel in any way. For
> information about official builds, see the [PerfView Download Page](documentation/Downloading.md) page.

### Contributing to PerfView 

You can get a lot of value out of the source code base simply by being able to build the code yourself, debug
through it or make a local, specialized feature, but the real power of open source software happens when
you contribute back to the shared code base and thus help the community as a whole.   **While we encourage this it 
requires significantly more effort on your part**.   If you are interested in stepping up, see the 
[PerfView Contribution Guide](CONTRIBUTING.md) and [PerfView Coding Standards](documentation/CodingStandards.md) before you start.

### Code Organization 

The code is broken into several main sections:

  * PerfView - GUI part of the application
    * StackViewer - GUI code for any view with the 'stacks' suffix
    * EventViewer - GUI code for the 'events' view window
    * Dialogs - GUI code for a variety of small dialog boxes (although the CollectingDialog is reasonably complex)
    * Memory - Contains code for memory investigations, in particular it defines 'Graph' and 'MemoryGraph' which are used 
      to display node-arc graphs (e.g. GC heaps)
  * TraceEvent - Library that understands how to decode Event Tracing for Windows (ETW) which is used to actually 
  collect the data for many investigations
  * MainWindow - GUI code for the window that is initially launched (lets you select files or collect new data)
  * ETWClrProfiler* - There are two projects that build the same source either 32 or 64 bit.   This is (the only) native code
  project in PerfView, and implements the CLR Profiler API and emits ETW events. It is used to trace object allocation
  stacks and .NET method calls.  
  * HeapDump* There are 32 and 64 bit versions of this project.  These make standalone executables that can dump the GC
  heap using Microsoft.Diagnostics.Runtime APIs.  This allows getting heap dumps from debugger process dumps.  
  * Global - An example of using PerfView's extensibility mechanism
  * CSVReader - old code that lets PerfView read .ETL.CSV files generated by XPERF (probably will delete)
  * Zip - a clone of System.IO.Compression.dll so that PerfView can run on pre V4.5 runtimes (probably will delete)