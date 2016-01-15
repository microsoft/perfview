using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinuxEvent.LinuxTraceEvent
{
	internal class LinuxEvent
	{
		internal string Command { get; }
		internal int ThreadID { get; }
		internal int ProcessID { get; }
		internal double Time { get; }
		internal int TimeProperty { get; }
		internal int Cpu { get; }
		internal string EventName { get; }
		internal string EventProperty { get; }
		internal int SampleID { get; }

		internal LinuxEvent(string comm, int tid, int pid, double time, int timeProp, int cpu, string eventName, string eventProp, int sampleID)
		{
			this.Command = comm;
			this.ThreadID = tid;
			this.ProcessID = pid;
			this.Time = time;
			this.TimeProperty = timeProp;
			this.Cpu = cpu;
			this.EventName = eventName;
			this.EventProperty = eventProp;
			this.SampleID = sampleID;
		}
	}
}
