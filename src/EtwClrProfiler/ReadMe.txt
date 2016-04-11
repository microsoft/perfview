========================================================================
    DYNAMIC LINK LIBRARY : ETWClrProfiler Project Overview
========================================================================

Start in CorProfilerTracer.cpp.  This holds the class that implements the
.NET Profiler API.   Initialize is where it first gets called.

The Schema for the ETW events are in ETWClrProfiler.man.  

The COM Guid for the profiler is in ComInfrastructure.cpp.  

Logger.* is a DEBUG-ONLY utility routine that logs to a file 

ETWClrProfiler.h is a GENERATED file that came from the manifest 
(MC.EXE compiled it, see Stdafx.h for details of regenerating it)

