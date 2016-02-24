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
			// For some reason these things sometimes don't catch when a file is being used by another
			//   process. Is there a better way to check whether or not a file is still being used?
			//   I realize this solution might have been the result of an XY problem.
			Thread.Sleep(50);
			while (!IsFileReady(fileName))
			{
			}
		}

		public static bool IsFileReady(string fileName)
		{
			try
			{
				using (FileStream inputStream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None))
				{
					if (inputStream.Length > 0)
					{
						return true;
					}
					else
					{
						return false;
					}

				}
			}
			catch (IOException)
			{
				return false;
			}
		}
	}
}
