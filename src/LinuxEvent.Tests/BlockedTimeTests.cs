using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinuxEvent.LinuxTraceEvent;
using Xunit;

namespace LinuxEvent.Tests
{
	/// <summary>
	/// Completed Context Switch - Starts with a thread that blocked through a sched_switch event and ends with the same
	/// thread that was unblocked through a sched_switch.
	/// Induced Context Switch - Starts with a blocked thread through sched_switch, ends when a different thread is
	/// using the same CPU, in which we induce that the original thread was unblocked somewhere in between.
	/// Incomplete Context Switch - Starts with a thread that has been blocked through a sched_switch event and unblocked
	/// when the sample data is finished.
	/// </summary>

	public class BlockedTimeTests
	{
		private void TotalBlockedTimeTest(string source, double expectedTotalBlockedPeriod)
		{
			PerfScriptEventParser parser = new PerfScriptEventParser(source, true);
			parser.Parse(null, 100, testing: true);

			Assert.Equal(expectedTotalBlockedPeriod, parser.GetTotalBlockedTime());
		}

		[Fact]
		public void NoTimeBlocked1()
		{
			string path = Constants.GetPerfDumpPath("onegeneric");
			this.TotalBlockedTimeTest(path, expectedTotalBlockedPeriod: 0.0);
		}

		[Fact]
		public void OneCompletedContextSwitch()
		{
			string path = Constants.GetPerfDumpPath("one_complete_switch");
			this.TotalBlockedTimeTest(path, expectedTotalBlockedPeriod: 1.0);
		}

		[Fact]
		public void OneInducedContextSwitch()
		{
			string path = Constants.GetPerfDumpPath("one_induced_switch");
			this.TotalBlockedTimeTest(path, expectedTotalBlockedPeriod: 1.0);
		}

		[Fact]
		public void OneIncomplateContextSwitch()
		{
			string path = Constants.GetPerfDumpPath("one_incomplete_switch");
			this.TotalBlockedTimeTest(path, expectedTotalBlockedPeriod: 1.0);
		}

		[Fact]
		public void NoTimeBlocked2_Induced()
		{
			string path = Constants.GetPerfDumpPath("notimeblocked_induced");
			this.TotalBlockedTimeTest(path, expectedTotalBlockedPeriod: 2.0);
		}
	}
}
