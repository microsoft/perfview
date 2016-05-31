# PerfView
PerfView is a performance-analysis tool that helps isolate CPU- and memory-related performance issues.

If you are unfamiliar with PerfView, there are [PerfView video tutorials](http://channel9.msdn.com/Series/PerfView-Tutorial). 
As well as [Vance Morrison's blog](http://blogs.msdn.com/b/vancem/archive/tags/perfview) which also gives overview and getting 
started information. 

The PerfView executable is ultimately published at the 
[PerfView download Site](http://www.microsoft.com/en-us/download/details.aspx?id=28567). 
It is a standalone executable file (packaged in a ZIP archive). You can be running it in less than a minute!  

The PerfView user's guide is part of the application itself, however you can get the .HTM file for it in 
the user's guide in the source code itself at [PerfView/SupportDlls/UsersGuide.htm](src/PerfView/SupportDlls/UsersGuide.htm) or
[the raw view](https://raw.githubusercontent.com/Microsoft/perfview/master/src/PerfView/SupportDlls/UsersGuide.htm?token=AIEUlpLp2aAS0_OgCbvDPMOz6U6leXDvks5XHMNFwA%3D%3D)
however it is a significantly better experience if you simply download PerfView and select the Help -> User's Guide menu item.  

###Cloning the PerfView GitHub Repository. 
If you are already familiar with how GIT, GitHub, and Visual Studio 2015 GIT support works, than you can skip this section.
However if not the [Setting up a Local GitHub repository with Visual Studio 2015](documentation/SettingUpRepoInVS2015.md) document
will lead you through the basics of doing this.   All it assumes is that you have Visual Studio 2015 installed.  These instructions
should also mostly work for VS 2013 with GIT extensions installed, but that has not been field-tested).   

###How to Build and Debug PerfView 
PerfView is designed to build in Visual Studio 2013 or later.  

  * The solution file is src/PerfView/Perfview.sln.  Opening this file in Visual file and selecting the Build -> Build Solution, 
  will build it.   It follows standard Visual Studio conventions, and the resulting PerfView.exe file ends up in the 
  src/PerfView/bin/<BuildType>/PerfView.exe   You need only deploy this one EXE to use it.  

  * The solution consists of 11 projects, representing support DLLs are the main EXE.   To run PerfView in the 
  debugger (F5) **you need to make sure that the 'Startup Project' is set to the 'PerfView' project** so that it launches 
  the main EXE.   If the PerfView project is not bold, right click on the PerfView project in the 'Solution  
  Explorer (on right) and select 'Set as Startup Project'.    After doing this 'Start Debugging' (F5) should work.   
  (it is annoying that this is not part of the .sln file...).  

###Deploying your new version of Perfview
You will want to deploy the 'Release' rather than the 'Debug' version of PerfView.  Thus first set your build configuration to 'Release' (Text window in the top toolbar, or right click on the .SLN file -> Configuration Manager -> Active Solution Configuration).
Next build (Build -> Build Solution (Ctr-Shift-B)).   The result will be that in the src\perfView\bin\Release directory will be among other things  a PerfView.exe.   This one file is all you need to deploy.   Simply copy it to where you wish to deploy the app.  

####Information for build troubleshooting.  
  * One of the unusual things about PerfView is that it incorporates its support DLL into the EXE itself, and these get 
  unpacked on first launch.  This means that there are tricky dependencies in the build that are not typical.    You will 
  see errors that certain DLLs can't be found if there were build problems earlier in the build.   Typically you can fix 
  this simply by doing a normal (non-clean) build, since the missing file will be present from the last compilation.     
  If this does not fix things, see if the DLL being looked for actually exists (if it does, then rebuilding should fix it).   
  It can make sense to go down the project one by one and build them individually to see which one fails 'first'.  
  
  * Another unusual thing about PerfView is that it includes an extension mechanism complete with samples.   
  This extensions is the 'Global' project (Called that because it is the Global Extension whose commands don't have an
  expliict 'scope') and needs to refer to PerfView to resolve some of its references.   Thus you will get many 'not found' 
  issues in the 'Global' project.  These can be ignored until you get every other part of the build working. 

  * One of the invariants of the repo is that if you are running VS 2015 and you simply sync and build the PerfView.sln
  file, it is supposed to 'just work'.   If that does not happen, and the advice above does not help, then we need to
  either fix the repo or update the advice above.   Thus it is reasonable to open an issue.   If you do this, the goal
  is to fix the problem, which means you have to put enough information into the issue to do that.   This includes 
  exactly what you tried, and what the error messages were.   

### Contributing to PerfView 

You can get a lot of value out of the source code base simply by being able to build the code yourself, debug
through it or make add a local, specialized feature.    But the real power of open source software happens when
you contribute back to shared code base and thus help the community as a whole.   **while we encourage this it 
requires significantly more effort on your part**.   If you are interested in stepping up, see the 
[PerfView Contribution Guide](CONTRIBUTING.md) and [PerfView Coding Standards](documentation/CodingStandards.md) before you start.  
###Code Organization 

The code is broken in several main sections:
  * TraceEvent - Library that understands how to decode Event Tracing for Windows (ETW) which is used to actually 
  collect the data for many investigations
  * PerfView - GUI part of the application
  * MainWindow - GUI code for the window that is initially launched (lets you select files or collect new data) 
  * StackViewer - GUI code for any view with the 'stacks' suffix
  * EventViewer - GUI code for the 'events' view window
  * Dialogs - GUI code for a variety of small dialog boxes (although the CollectingDialog is reasonably complex)
  * Memory - Contains code for memory investigations, in particular it defines 'Graph' and 'MemoryGraph' which are used 
  to display node-arc graphs (e.g. GC heaps)
  * [HtmlJs](src\HtmlJs\Readme.md) - contains a version of the GUI based on HTML and JavaScript (for Linux support).  
s
