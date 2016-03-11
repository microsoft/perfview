using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Diagnostics.Tracing.StackSources;
using Microsoft.Diagnostics.Tracing.Stacks;
using Xunit;

namespace LinuxTracing.Tests
{
	public class InterningTests
	{
		private void InterningStackCountTest(string source, int expectedStackCount)
		{
			Constants.WaitUntilFileIsReady(source);

			ParallelLinuxPerfScriptStackSource stackSource = new ParallelLinuxPerfScriptStackSource(source);
			Assert.Equal(expectedStackCount, stackSource.Interner.CallStackCount);
		}

		[Fact]
		public void OneSample()
		{
			string path = Constants.GetTestingPerfDumpPath("onegeneric");
			this.InterningStackCountTest(path, expectedStackCount: 3);
		}

		[Fact]
		public void TwoSameSamples()
		{
			string path = Constants.GetTestingPerfDumpPath("twogenericsame");
			this.InterningStackCountTest(path, expectedStackCount: 3);
		}

		[Fact]
		public void TwoSameLongSamples()
		{
			string path = Constants.GetTestingPerfDumpPath("twogenericsamelongstacks");
			this.InterningStackCountTest(path, expectedStackCount: 8);
		}

		[Fact]
		public void TwoAlteredLongSamples()
		{
			string path = Constants.GetTestingPerfDumpPath("twodifferentlongstacks");
			this.InterningStackCountTest(path, expectedStackCount: 10);
		}
	}
}
