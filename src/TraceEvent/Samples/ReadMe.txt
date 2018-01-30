
******************************************************************************
********  Welcome to the Microsoft TraceEvent Samples NUGET package  *********
 
**** QUICK START

To run all the samples, simply have your main program call

	 TraceEventSamples.AllSamples.Run();

The samples are all under the 'TraceEventSamples' folder in your solution and
there is a file for each sample of the form NN_<SampleName>.cs.   The samples
have detailed comments that tell what they do as well as WriteLine statements
that also indicate what is going on.   

So you can either simply run all the samples, or take a look at the comments
in each one to see which one is most appropriate for your needs.  Each sample
has a 'Run' method that is is main entry point, so it is easy to run just
one of the samples.   For example

	TraceEventSamples.SimpleEventSourceMonitor.Run();

Will run just the SimpleEventSourceMonitor sample.   

The Sample package also includes the _TraceEventProgrammersGuide.docx file
that gives you an overview of the package as a whole and how it is intended
to be used.   It is worth reading.  

The TraceEvent package was also included when the TraceEvent Samples package
was selected.   Please the the TraceEvent.ReadMe.txt file for more.

******************************************************************************

A useful technique is to install this package into the program that you are 
working on, cut and paste some of the sample code to your own code, and then 
uninstall the sample package (which will remove all the code you did not cut
and paste).   

By default the output goes to Console.Out but you can redirect it to another
TextWriter by setting AllSamples.Out.  This is useful for GUI Apps.  

******************************************************************************

