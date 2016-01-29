using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinuxTracing.LinuxTraceEvent
{
	public class ScheduledEvent : LinuxEvent
	{
		public static readonly string Name = "sched_switch";

		public ScheduleSwitch Switch { get; }

		public ScheduledEvent(
			string comm, int tid, int pid,
			double time, double period, int timeProp, int cpu,
			string eventName, string eventProp, IEnumerable<Frame> callerStacks, ScheduleSwitch schedSwitch) :
			base(comm, tid, pid, time, period, timeProp, cpu, eventName, eventProp, callerStacks)
		{
			this.Switch = schedSwitch;
		}
	}

	public class ScheduleSwitch
	{
		public string PreviousCommand { get; }
		public int PreviousThreadID { get; }
		public int PreviousPriority { get; }
		public char PreviousState { get; }
		public string NextCommand { get; }
		public int NextThreadID { get; }
		public int NextPriority { get; }

		public ScheduleSwitch(string prevComm, int prevTid, int prevPrio, char prevState, string nextComm, int nextTid, int nextPrio)
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

	public class LinuxEvent
	{
		public string Command { get; }
		public int ThreadID { get; }
		public int ProcessID { get; }
		public double Time { get; }
		public double Period { get; }
		public int TimeProperty { get; }
		public int Cpu { get; }
		public string EventName { get; }
		public string EventProperty { get; }
		public IEnumerable<Frame> CallerStacks { get; }

		public LinuxEvent(
			string comm, int tid, int pid,
			double time, double period, int timeProp, int cpu,
			string eventName, string eventProp, IEnumerable<Frame> callerStacks)
		{
			this.Command = comm;
			this.ThreadID = tid;
			this.ProcessID = pid;
			this.Time = time;
			this.Period = period;
			this.TimeProperty = timeProp;
			this.Cpu = cpu;
			this.EventName = eventName;
			this.EventProperty = eventProp;
			this.CallerStacks = callerStacks;
		}
	}
}
