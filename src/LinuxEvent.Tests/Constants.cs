using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinuxTracing.Tests
{
	public static class Constants
	{
		public static readonly string SourceFolder = @"Sources\";

		public static string GetFilePath(string filename)
		{
			return Path.Combine(Environment.CurrentDirectory, SourceFolder, filename);
		}

		public static string GetPerfDumpPath(string filename)
		{
			return Path.Combine(Environment.CurrentDirectory, SourceFolder, string.Format("{0}.perf.data.dump", filename));
		}
	}
}
