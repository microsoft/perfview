using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LinuxTracing.Tests
{
	public static class Constants
	{
		public static readonly string SourceFolder = @"Sources\";
		public static readonly string OutputFolder = @"Outputs\";

		public static string GetTestingFilePath(string filename)
		{
			return Path.Combine(Environment.CurrentDirectory, SourceFolder, filename);
		}

		public static string GetTestingPerfDumpPath(string filename)
		{
			return Path.Combine(Environment.CurrentDirectory, SourceFolder, string.Format("{0}.perf.data.dump", filename));
		}

		public static string GetOutputPath(string filename)
		{
			return Path.Combine(Environment.CurrentDirectory, OutputFolder, string.Format("{0}", filename));
		}

		public static void WaitUntilFileIsReady(string fileName)
		{
            // This used to have logic that polled until the file existed.
            // This seems like a hack, and the polling logic happened to
            // open the file read-write, which caused file sharing conflicts
            // with concurrent tests.    

            // Note that on windows even doing a File.Exists(XXX) LOCKS the
            // file for a short time.   THus we don't want to do things like
            // that since it can interfer (this is unfortunate).  If we need
            // a file probe, we need to do it with a FileOpen with appropriate 
            // File sharing attributes.  

            // None of this should be necessary.   I am reomving it and if
            // This method should go away if we don't have problems with it 
            // for a while. 
            // -- vance 5/17/2017

            // Thread.Sleep(2);
            
		}
	}
}
