using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Diagnostics.Tracing.StackSources;
using Xunit;

namespace LinuxTracing.Tests
{
	public class XmlWriting
	{
		private static void Write(string source)
		{
			var stackSource = new LinuxPerfScriptStackSource(source);
			XmlStackSourceWriter.WriteStackViewAsZippedXml(stackSource,
				Constants.GetOutputPath(Path.GetFileNameWithoutExtension(source) + ".perfView.xml.zip"));
		}

		[Fact]
		public void SimpleDump()
		{
			Write(Constants.GetTestingFilePath(@"C:\Users\t-lufern\Desktop\Luca\dev\longtrace2.perf.data.dump"));
		}
	}
}
