using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinuxEvent.LinuxTraceEvent
{
	internal class ScheduledEvent : LinuxEvent
	{
		internal static readonly string Name = "sched_switch";

		internal ScheduleSwitch Switch { get; }

		internal ScheduledEvent(
			string comm, int tid, int pid,
			double time, int timeProp, int cpu,
			string eventName, string eventProp, int sampleID, ScheduleSwitch schedSwitch) :
			base(comm, tid, pid, time, timeProp, cpu, eventName, eventProp, sampleID)
		{
			this.Switch = schedSwitch;
		}
	}

	internal class ScheduleSwitch
	{
		internal string PreviousCommand { get; }
		internal int PreviousThreadID { get; }
		internal int PreviousPriority { get; }
		internal char PreviousState { get; }
		internal string NextCommand { get; }
		internal int NextThreadID { get; }
		internal int NextPriority { get; }

		internal ScheduleSwitch(string prevComm, int prevTid, int prevPrio, char prevState, string nextComm, int nextTid, int nextPrio)
		{
			this.PreviousCommand = prevComm;
			this.PreviousThreadID = prevTid;
			this.PreviousPriority = prevPrio;
			this.PreviousState = prevState;
			this.NextCommand = nextComm;
			this.NextThreadID = nextTid;
			this.NextPriority = nextPrio;
		}
	}

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

		internal LinuxEvent(
			string comm, int tid, int pid,
			double time, int timeProp, int cpu,
			string eventName, string eventProp, int sampleID)
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
