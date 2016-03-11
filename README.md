# PerfView
PerfView is a performance-analysis tool that helps isolate CPU- and memory-related performance issues.

If you are unfamiliar with PerfView, there are [PerfView video tutorials](http://channel9.msdn.com/Series/PerfView-Tutorial). As well as [Vance Morrison's blog](http://blogs.msdn.com/b/vancem/archive/tags/perfview) which also gives overview and getting started information. 

The PerfView executable is ultimately published at the [PerfView download Site](http://www.microsoft.com/en-us/download/details.aspx?id=28567). It is a standalone executable file (packaged in a ZIP archive). You can be running it in less than a minute!  

The PerfView users guide is part of the application itself, however you can get the .HTM file for it in the users guide in the soruce code itself at [PerfView/SupportDlls/UsersGuide.htm](src/PerfView/SupportDlls/UsersGuide.htm), however it is a significantly better experience if you simply download Perfview and select the Help -> User's Guide menu item.  

###How to Build and Debug PerfView 
PerfView is designed to build in Visual Studio 2013 or later.  

  * The solution file is src/PerfView/Perfview.sln.  Opening this file in Visual file and selecting the Build -> Build Solution, will build it.   It follows standard Visual Studio conventions, and the resulting PerfView.exe file ends up in the src/PerfView/bin/<BuildType>/PerfView.exe   You need only deploy this one EXE to use it.  
  * The solution consists of 11 projects, representing support DLLs are the main EXE.   To run PerfVIew in the debugger (F5) **you need to make sure that the 'Startup Project' is set to the 'PerfView' project** so that it launches the main EXE.   If the PerfView project is not bold, right click on the PerfView project in the 'Solution  Explorer (on right) and select 'Set as Startup Project'.    After doing this 'Start Debugging' (F5) should work.   (it is annoying that this is not part of the .sln file...).  

####Information for build troubleshooting.  
* One of the unusual things about PerfView is that it incoperates its support DLL into the EXE itself, and these get unpacked on first launch.  This means that there are tricky dependencies in the build that are not typical.    You will see errors that certain DLLs can't be found if there were build problems earlier in the build.   Typially you can fix this simply by doing a nonrmal (non-clean) build, since the missing file will be present from the last compilation.     If this does not fix things, See if the DLL being looked for actualy exist (if it does, then rebuilding should fix it).   it can make sense to go down the project one by one and build them individuallly to see which one fails 'first'.  
* Another unusual thing about PerfView is that it includes an extention mechanism complete with samples of using that.   This extenions is the 'Global' project (Called that because it is the Global Extension whose commands don't have a 'scope') and needs to refer to PerfView to resolve some of its references.   Thus you will get many 'not found' issues in the 'Global' project.  These can be ignored until you get every other part of the build working. 

###Code Organization
The code is broken in several main sections:
  * TraceEvent - Library that understands how to decode Event Tracing for Windows (ETW) which is used to actually collect the data for many investgations
  * PerfView - GUI part of the application
  * MainWindow - GUI code for the window that is initially launched (lets you select files or collect new data) 
  * StackViewer - GUI code for any view with the 'stacks' suffix
  * EventViwer - GUI code for the 'events' view window
  * Dialogs - GUI code for a variety of small dialog boxes (although the CollectingDialog is reasonably complex)
  * Memory - Contains code for memory investigations, in particular it defines 'Graph' and 'MemoryGraph' which are used to desiplay node-arc graphs (e.g. GC heaps)
