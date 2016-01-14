using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinuxEvent.LinuxTraceEvent;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace LinuxPerfView.LinuxTraceEvent
{
	/*
	internal sealed class PerfScriptProfilerSources : StackSource
	{

		internal PerfScriptProfilerSources(string filePath)
		{
			this.sourceFilePath = filePath;
			this.parser = new PerfScriptTraceEventParser();
			this.parser.Parse(this.sourceFilePath);
		}

		public override int CallFrameIndexLimit
		{
			get
			{
				return this.parser.FrameID;
			}
		}

		public override int CallStackIndexLimit
		{
			get
			{
				return this.parser.StackID;
			}
		}

		public override void ForEach(Action<StackSourceSample> callback)
		{
			var sample = new StackSourceSample(this);
			for (int i = 0; i < this.parser.SampleID; i++)
			{
				sample.SampleIndex = (StackSourceSampleIndex)i;
				sample.StackIndex = parser.Samples[(int)sample.SampleIndex].TopStackID;
				sample.TimeRelativeMSec = parser.Samples[(int)sample.SampleIndex].Time;
				callback(sample);
			}
		}

		public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
		{
			return this.parser.Stacks[(int)callStackIndex].CallerID;
		}

		public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
		{
			return this.parser.Stacks[(int)callStackIndex].FrameID;
		}

		public override string GetFrameName(StackSourceFrameIndex frameIndex, bool verboseName)
		{
			return this.parser.IDToFrame[(int)frameIndex].Name;
		}


		private readonly PerfScriptTraceEventParser parser;
		private readonly string sourceFilePath;
	}
	*/
}
