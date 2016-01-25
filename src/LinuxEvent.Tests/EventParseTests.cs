using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinuxEvent.LinuxTraceEvent;
using Xunit;

namespace LinuxEvent.Tests
{
	public class EventParseTests
	{
		private void DoStackTraceTest(string source, List<List<string>> moduleSymbolsCallerStack)
		{
			PerfScriptEventParser parser = new PerfScriptEventParser(source, false);
			parser.Parse(null, 100, testing: true);

			for (int sample = 0; sample < parser.SampleCount; sample++) {
				int stackID = parser.GetStackAtSample(sample);
				for (int i = 0; i < moduleSymbolsCallerStack[sample].Count; i++)
				{
					if (stackID == -1)
					{
						Assert.Equal(moduleSymbolsCallerStack[sample][i], null);
						continue;
					}

					int actualFrame = parser.GetFrameAtStack(stackID);
					string actualName = parser.GetFrameAt(actualFrame);
					Assert.Equal(moduleSymbolsCallerStack[sample][i], actualName);
					stackID = parser.GetCallerAtStack(stackID);
				}
			}
		}

		[Fact]
		public void OneStack()
		{
			string path = Constants.GetPerfDumpPath("onegeneric");
			this.DoStackTraceTest(path, new List<List<string>> {
				new List<string>{ "module!symbol", "Thread (0)", "comm (0)", null }
			});
		}
	}
}
