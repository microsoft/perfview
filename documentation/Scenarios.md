# PerfView vs. TraceEvent

Not sure which you should use?  This document aims to point you in the right direction.

## Start With PerfView If Your Goal Is To...
 - Collect an adhoc Event Tracing for Windows (ETW) trace to analyze program behavior or a performance issue.
 - Collect an adhoc heap snapshot to analyze a managed memory issue such as a managed memory leak.
 - Use the flight recorder mode to capture an ETW trace of hard to reproduce behavior.
 - Perform adhoc analysis of a previously collected performance trace.
 - Diff two performance traces to or managed memory heap snapshots to root cause a performance issue.
 - Use a GUI-based performance analysis tool.

## Start With TraceEvent If Your Goal Is To...
 - Have programmatic access to trace collection and/or trace processing and analysis.
 - Implement a service that captures or processes traces at scale.
 - Build collection and/or processing capabilities into an existing application.

## PerfView Limitations
 - PerfView is not designed to be used as a capture or processing agent in services.  It is designed for use in user-interactive sessions.
 - PerfView is not supported on operating system SKUs such as nanoserver that do not have GUI libraries installed.  See PerfViewCollect if you need to capture adhoc traces on these SKUs.