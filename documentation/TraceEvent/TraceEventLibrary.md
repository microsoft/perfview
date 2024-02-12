# The Microsoft.Diagnostics.Tracing.TraceEvent Library

The TraceEvent library is a library that allows you to both collect and process event data. It was originally designed to parse Event Tracing for Windows (ETW) events that the Windows operating system can generate. This is the library that PerfView uses to do most of its data manipulations, so if you are trying to automate the processing of some data that you can see in PerfView, there is a very good chance that you want to use the TraceEvent library to do it.

Microsoft.Diagnostics.Tracing.TraceEvent is a [NuGet package](https://www.nuget.org/packages/Microsoft.Diagnostics.Tracing.TraceEvent/) available from nuget.org. This library works on both the .NET Desktop (v4.6.2 and up) as well a .NET (or .NET Core) (netstandard2.0 and up). Parts of the library work on Linux, but ETW is a Windows specific technology and thus that part (which is a lot of the package) only works on Windows.

The data model for ETW data is non-trivial, and the data is large (which means that you have to care about efficiency). There is also the question of decoding events (they are logged as binary blobs) as well as the parsing of stack information (which needs symbol files or .NET Events to decode properly). This makes the _data model_ exposed by the TraceEvent package non-trivial and requires some explanation.

[TraceEvent Programmers Guide](./TraceEventProgrammersGuide.md) is your guide for this. Fundamentally however, the traceEvent library simply provides a set of .NET class definitions as a NuGet package. Thus you only need to create a .NET project (it can be .NET or .NET Desktop), add a reference to the Microsoft.Diagnostics.Tracing.TraceEvent NuGet package (right click on the project -> Manage NuGet Packages) and start using the APIs. Here is a trivial example that logs all the kernel, CLR and dynamic events in an ETL (ETW Trace File).

```
using Microsoft.Diagnostics.Tracing;
using System;

class Program
{
    static void Main()
    {
        using (var source = new ETWTraceEventSource("ETWData.etl"))
        {
            // setup the callbacks
            source.Clr.All += Print;
            source.Kernel.All += Print;
            source.Dynamic.All += Print;

            // iterate over the file, calling the callbacks.
            source.Process();
        }
    }

    static void Print(TraceEvent data)
    {
        Console.WriteLine(data.ToString());
    }
}
```

## TraceEvent Samples

To see more complete samples that use the APIs in more sophisticated (but not too sophisticated ways). In the [Samples directory](https://github.com/Microsoft/perfview/tree/main/src/TraceEvent/Samples) here are one or two page samples doing interesting things (collecting data, parsing from files or in real time, transforming one ETL file to another etc). Each sample is independent of the others.

One way of getting the samples is to walk through the step by step guide in [Vance's Walkthough on TraceEvent](https://blogs.msdn.microsoft.com/vancem/2014/03/15/walk-through-getting-started-with-etw-traceevent-nuget-samples-package/). This walkthrough uses TraceEvent Samples package, however this code is old (but still completely relevant
the APIs have not changed). These samples are exactly the samples in Github mentioned above.

The easiest way to build the latest samples is to simply clone the [PerfView Repository](https://github.com/Microsoft/perfview) and and build it. The samples are in the _TraceEventSamples_ project in the PerfView solution. Simply set this project to be your _Startup Project_ (right click on it in Solution Explorer -> Set as Startup Project) and run it (F5). By default will run all the samples.

## Release Notes for the Microsoft.Diagnostics.Tracing.TraceEvent library

The [Releases](https://github.com/Microsoft/perfview/releases) page for PerfView also shows the releases for the TraceEvent library. PerfView's version numbers and TraceEvent version numbers starting with version 3 are kept in lock step.
