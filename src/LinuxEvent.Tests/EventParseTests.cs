using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinuxTracing.LinuxTraceEvent;
using Xunit;

namespace LinuxTracing.Tests
{
	public class EventParseTests
	{
		private void DoStackTraceTest(string source, bool blockedTime, List<List<string>> moduleSymbolsCallerStack)
		{
			PerfScriptEventParser parser = new PerfScriptEventParser(source, blockedTime);
			parser.Parse(null, 100, testing: true);

			for (int sample = 0; sample < parser.SampleCount; sample++)
			{
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

		private void HeaderTest(string source, bool blockedTime,
			string[] commands,
			int[] pids,
			int[] tids,
			int[] cpus,
			double[] times,
			int[] timeProperties,
			string[] events,
			string[] eventProperties)
		{
			PerfScriptEventParser parser = new PerfScriptEventParser(source, blockedTime);
			parser.Parse(null, 100, testing: true);

			// Need to make sure we have the same amount of samples
			Assert.Equal(commands.Length, parser.SampleCount);

			for (int i = 0; i < parser.SampleCount; i++)
			{
				LinuxEvent linuxEvent = parser.GetLinuxEventAt(i);
			}

		}

		[Fact]
		public void OneStack()
		{
			string path = Constants.GetPerfDumpPath("onegeneric");
			this.DoStackTraceTest(path, false, new List<List<string>> {
				new List<string>{ "module!symbol", "Thread (0)", "comm (0)", null }
			});
		}

		[Fact]
		public void LargeStack()
		{
			string path = Constants.GetPerfDumpPath("two_small_generic");
			this.DoStackTraceTest(path, false, new List<List<string>>
			{
				new List<string> { "module!symbol", "module2!symbol2", "main!main", "Thread (0)", "comm (0)" },
				new List<string> { "module3!symbol3", "module4!symbol4", "main!main", "Thread (0)", "comm2 (0)" }
			});
		}

		[Fact]
		public void NonSchedHeader()
		{
			string path = Constants.GetPerfDumpPath("onegeneric");
			this.HeaderTest(path, blockedTime: false,
			commands: new string[] { "comm" },
			pids: new int[] { 0 },
			tids: new int[] { 0 },
			cpus: new int[] { 0 },
			times: new double[] { 0.0 },
			timeProperties: new int[] { 0 },
			events: new string[] { "event_name" },
			eventProperties: new string[] { "event_properties" }); 
		}
	}
}
