# PerfView
PerfView is a performance-analysis tool that helps isolate CPU- and memory-related performance issues.

If you are unfamiliar with PerfVIew, there are [PerfView video tutorials](http://channel9.msdn.com/Series/PerfView-Tutorial).   As well as [Vance Morrison's blog](http://blogs.msdn.com/b/vancem/archive/tags/perfview) which also gives overview and getting started information. 

The PerfView executable is ultimately published at the [PerfView download Site](http://www.microsoft.com/en-us/download/details.aspx?id=28567).    It is one xcopy deployable EXE file (packaged in a ZIP file).  You can be running it in less than a minute, by clicking through the download site.  

The PerfView users guide is part of the application itself, however you can get at the .HTM file for it in the users guide in the soruce code itself at [PerfView/SupportDlls/UsersGuide.htm](https://github.com/Microsoft/perfview/blob/master/src/PerfView/SupportDlls/UsersGuide.htm).

The code itself is broken in several main section
* TraceEvent - This is the library that understands how to decode Event Tracing for Windows (ETW) which is used to actually collect the data for many investgations.
  * PerfView - The GUI part of the application
  * MainWindow - The GUI code for the window that is initially launched (lets you select files or collect new data) 
  * StackViewer - The GUI code for any view with the 'stacks' suffix
  * EventViwer - The GUI code for the 'events' view window
  * Dialogs - GUI code for a variety of small dialog boxes (although the CollectingDialog is reasonably complex) 
  * Memory - Contains code for memory investigations, in particular it defines 'Graph' and 'MemoryGraph' which are used to desiplay node-arc graphs (e.g. GC heaps).
