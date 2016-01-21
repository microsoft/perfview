using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClrProfiler;
using LinuxPerfView.Shared;
using Validation;

namespace LinuxEvent.LinuxTraceEvent
{

	internal class PerfScriptTraceEventParser
	{

		internal int FrameCount = 0;
		internal int StackCount = 0;
		internal int SampleCount = 0;

		// Optimized later to only have arrays of each of these types...
		private Dictionary<string, int> FrameID;
		private List<FrameInfo> IDFrame;
		private Dictionary<int, StackNode> Stacks;
		private Dictionary<long, FrameStack> FrameStacks;
		private List<SampleInfo> Samples;

		private Dictionary<int, ProcessNode> ProcessNodes;
		private Dictionary<long, ThreadNode> ThreadNodes;

		private Dictionary<int, ThreadInfo> BlockedThreads;
		private Dictionary<int, double> ThreadTimes;
		private List<ThreadInfo> OmittedThreads;

		private StackNode BlockedNode { get; set; }
		private StackNode CPUNode { get; set; }

		private double CurrentTime { get; set; }

		private bool startTimeSet = false;
		private double StartTime { get; set; }

		private bool TrackBlockedTime { get; set; }

		internal PerfScriptTraceEventParser(string sourcePath)
		{
			Requires.NotNull(sourcePath, nameof(sourcePath));

			this.FrameID = new Dictionary<string, int>();
			this.IDFrame = new List<FrameInfo>();
			this.Stacks = new Dictionary<int, StackNode>();
			this.Stacks.Add(-1, new FrameStack(-1, -1, null));
			this.FrameStacks = new Dictionary<long, FrameStack>();
			this.Samples = new List<SampleInfo>();

			this.events = new List<LinuxEvent>();
			this.source = new FastStream(sourcePath);

			this.ProcessNodes = new Dictionary<int, ProcessNode>();
			this.ThreadNodes = new Dictionary<long, ThreadNode>();

			this.BlockedThreads = new Dictionary<int, ThreadInfo>();
			this.OmittedThreads = new List<ThreadInfo>();
			this.ThreadTimes = new Dictionary<int, double>();
		}

		private void AddCPUAndBlockedFrames()
		{
			int frameCount = this.FrameCount++;
			int stackCount = this.StackCount++;

			this.FrameID.Add("Blocked", frameCount);
			this.IDFrame.Add(new BlockedCPUFrame(frameCount, "Blocked"));

			this.BlockedNode = new BlockedCPUNode(StackKind.Blocked, stackCount, frameCount, this.Stacks[-1]);
			this.Stacks.Add(stackCount, BlockedNode);


			frameCount = this.FrameCount++;
			stackCount = this.StackCount++;

			this.FrameID.Add("CPU", frameCount);
			this.IDFrame.Add(new BlockedCPUFrame(frameCount, "CPU"));

			this.CPUNode = new BlockedCPUNode(StackKind.CPU, stackCount, frameCount, this.Stacks[-1]);
			this.Stacks.Add(stackCount, CPUNode);
		}

		internal void Parse(string regexFilter, int maxSamples, bool blockedTime)
		{
			this.TrackBlockedTime = blockedTime;

			if (this.TrackBlockedTime)
			{
				this.AddCPUAndBlockedFrames();
			}

			Regex rgx = regexFilter == null ? null : new Regex(regexFilter);
			foreach (LinuxEvent linuxEvent in this.NextEvent(rgx))
			{
				if (linuxEvent != null)
				{
					this.events.Add(linuxEvent);
				}

				if (this.SampleCount > maxSamples)
				{
					break;
				}
			}

			this.FlushBlockedThreads();
		}

		/// <summary>
		/// Gets the string representation of the frame
		/// </summary>
		/// <param name="i">The location of the frame in the array</param>
		/// <returns>A string representing the frame in the form {module}!{symbol}</returns>
		internal string GetFrameAt(int i)
		{
			return this.IDFrame[i].DisplayName;
		}

		/// <summary>
		/// Gets the caller's ID at the given stack ID
		/// </summary>
		/// <param name="i">The stack ID where the caller ID in question is</param>
		/// <returns>An integer for the caller ID</returns>
		internal int GetCallerAtStack(int i)
		{
			return this.Stacks[i].Caller.StackID;
		}

		/// <summary>
		/// Gets the frame ID for the stack at the given ID
		/// </summary>
		/// <param name="i">The ID of the stack with the frame ID in question</param>
		/// <returns>The ID of the frame on the stack</returns>
		internal int GetFrameAtStack(int i)
		{
			return this.Stacks[i].FrameID;
		}

		/// <summary>
		/// Gets the stack at the given sample ID
		/// </summary>
		/// <param name="i">The ID that holds the stack in question</param>
		/// <returns>The stack ID on the sample</returns>
		internal int GetStackAtSample(int i)
		{
			return this.Samples[i].TopStackID;
		}

		/// <summary>
		/// Gets the time at the given sample ID
		/// </summary>
		/// <param name="i">The ID that holds the time in question</param>
		/// <returns>A double representing the time since execution in milliseconds</returns>
		internal double GetTimeAtSample(int i)
		{
			return this.Samples[i].Time;
		}

		private IEnumerable<LinuxEvent> NextEvent(Regex regex)
		{

			string line = string.Empty;

			while (true)
			{

				this.source.SkipWhiteSpace();

				if (this.source.EndOfStream)
				{
					break;
				}

				EventKind eventKind = EventKind.General;

				StringBuilder sb = new StringBuilder();

				// comm
				this.source.ReadAsciiStringUpToWhiteSpace(sb);
				string comm = sb.ToString();
				if (comm.Length > 0 && comm[0] == 0)
				{
					comm = comm.Substring(1, comm.Length - 1);
				}
				sb.Clear();

				// Process ID
				this.source.SkipWhiteSpace();
				int pid = this.source.ReadInt();
				this.source.MoveNext();

				// Thread ID
				int tid = this.source.ReadInt();

				// CPU
				this.source.SkipWhiteSpace();
				this.source.MoveNext();
				int cpu = this.source.ReadInt();
				this.source.MoveNext();

				// Time
				this.source.SkipWhiteSpace();
				this.source.ReadAsciiStringUpTo(':', sb);
				double time = double.Parse(sb.ToString());
				sb.Clear();
				this.CurrentTime = time;
				if (!this.startTimeSet)
				{
					this.startTimeSet = true;
					this.StartTime = time;
				}

				// Time Property
				this.source.MoveNext();
				this.source.SkipWhiteSpace();
				int timeProp = this.source.ReadInt(); // for now we just move past it...

				// Event Name
				this.source.SkipWhiteSpace();
				this.source.ReadAsciiStringUpTo(':', sb);
				string eventName = sb.ToString();
				sb.Clear();
				this.source.MoveNext();

				// Event Properties
				// I mark a position here because I need to check what type of event this is without screwing up the stream
				var markedPosition = this.source.MarkPosition();
				this.source.ReadAsciiStringUpTo('\n', sb);
				string eventDetails = sb.ToString();
				sb.Clear();

				ScheduleSwitch scheduleSwitch = null;

				if (eventDetails.Length >= ScheduledEvent.Name.Length &&
					eventDetails.Substring(0, ScheduledEvent.Name.Length) == ScheduledEvent.Name)
				{
					// Since it's a schedule switch, it's easier to read the details through the stream rather than through
					//   string manipulation, so I move back
					this.source.RestoreToMark(markedPosition);

					eventKind = EventKind.Scheduled;
					scheduleSwitch = this.ReadScheduleSwitch();

					this.source.SkipUpTo('\n');


					if (this.TrackBlockedTime)
					{
						this.AnalyzeBlockedTime(scheduleSwitch, time);
					}
				}

				// In any case, we end up reading up to a new line symbol, so we need to move past it.
				this.source.MoveNext();


				// Period Between last thread sample
				double previousTime;
				double period;
				if (!this.ThreadTimes.TryGetValue(tid, out previousTime))
				{
					period = this.StartTime;
				}
				else
				{
					period = time - previousTime;
				}
				this.ThreadTimes[tid] = time;


				// Now that we know the header of the trace, we can decide whether or not to skip it given our pattern
				if (regex != null && !regex.IsMatch(eventName))
				{
					while (true)
					{
						this.source.MoveNext();
						if ((this.source.Current == '\n' &&
							(this.source.Peek(1) == '\n' || this.source.Peek(1) == '\r' || this.source.Peek(1) == 0)) ||
							 this.source.EndOfStream)
						{
							break;
						}
					}

					yield return null;
				}
				else
				{
					LinuxEvent linuxEvent;
					switch (eventKind)
					{
						case EventKind.Scheduled:
							{
								linuxEvent =
									new ScheduledEvent(comm, tid, pid, time, period, timeProp, cpu,
									eventName, eventDetails, this.SampleCount++, scheduleSwitch);
								break;
							}
						default:
							{
								linuxEvent =
									new LinuxEvent(comm, tid, pid, time, period, timeProp, cpu,
									eventName, eventDetails, this.SampleCount++);
								break;
							}
					}

					this.ReadStackTraceForEvent(linuxEvent);
					yield return linuxEvent;
				}
			}
		}

		private void FlushBlockedThreads()
		{
			foreach (int key in this.BlockedThreads.Keys)
			{
				ThreadInfo threadInfo = this.BlockedThreads[key];
				threadInfo.Unblock(this.CurrentTime);
				this.OmitUnblockedThread(threadInfo.ID);
			}
		}

		private void AnalyzeBlockedTime(ScheduleSwitch scheduleSwitch, double time)
		{
			// Check if a thread has been unblocked, here if a thread has been unblocked but we
			//   haven't seen it blocked, we'll just skip it and count threads like it as CPU (for now) TODO
			ThreadInfo threadInfo;
			if (this.BlockedThreads.TryGetValue(scheduleSwitch.NextThreadID, out threadInfo))
			{
				threadInfo.Unblock(time);
				this.OmitUnblockedThread(threadInfo.ID);
			}

			// Check if a thread has been blocked, if it has been blocked we unblock it, push out of the dictionary and
			//   re-add a new one because (we assume) the thread can't be "double" blocked
			if (this.BlockedThreads.TryGetValue(scheduleSwitch.PreviousThreadID, out threadInfo))
			{
				threadInfo.Unblock(time);
				this.OmitUnblockedThread(threadInfo.ID);
			}

			this.BlockedThreads.Add(scheduleSwitch.PreviousThreadID, new ThreadInfo(scheduleSwitch.PreviousThreadID));
		}

		private void OmitUnblockedThread(int threadID)
		{
			// If for some reason the thread ID is not in the dictionary, then something clearly went wrong
			//   so I'll let this method throw an exception
			this.OmittedThreads.Add(this.BlockedThreads[threadID]);
			this.BlockedThreads.Remove(threadID);
		}

		private ScheduleSwitch ReadScheduleSwitch()
		{
			StringBuilder sb = new StringBuilder();

			this.source.SkipUpTo('=');
			this.source.MoveNext();

			this.source.ReadAsciiStringUpTo(' ', sb);
			string prevComm = sb.ToString();
			sb.Clear();

			this.source.SkipUpTo('=');
			this.source.MoveNext();

			int prevTid = this.source.ReadInt();

			this.source.SkipUpTo('=');
			this.source.MoveNext();

			int prevPrio = this.source.ReadInt();

			this.source.SkipUpTo('=');
			this.source.MoveNext();

			char prevState = (char)this.source.Current;

			this.source.MoveNext();
			this.source.SkipUpTo('n'); // this is to bypass the ==>
			this.source.SkipUpTo('=');
			this.source.MoveNext();

			this.source.ReadAsciiStringUpTo(' ', sb);
			string nextComm = sb.ToString();
			sb.Clear();

			this.source.SkipUpTo('=');
			this.source.MoveNext();

			int nextTid = this.source.ReadInt();

			this.source.SkipUpTo('=');
			this.source.MoveNext();

			int nextPrio = this.source.ReadInt();

			return new ScheduleSwitch(prevComm, prevTid, prevPrio, prevState, nextComm, nextTid, nextPrio);
		}

		private void ReadStackTraceForEvent(LinuxEvent linuxEvent)
		{
			this.DoStackTrace(linuxEvent);
			this.Samples.Add(new SampleInfo(this.StackCount - 1, linuxEvent.Time));
		}

		private StackNode GetTopCaller(int threadID)
		{
			if (!this.TrackBlockedTime)
			{
				return this.Stacks[-1];
			}

			ThreadInfo threadInfo;
			if (this.BlockedThreads.TryGetValue(threadID, out threadInfo))
			{
				return BlockedNode;
			}

			return CPUNode;
		}

		private StackNode DoStackTrace(LinuxEvent linuxEvent)
		{

			// This is the base case for the stack trace, when there are no more "real" frames (i.e. an empty line)
			if ((this.source.Current == '\n' &&
				(this.source.Peek(1) == '\n' || this.source.Peek(1) == '\r' || this.source.Peek(1) == '\0') ||
				 this.source.EndOfStream))
			{
				this.source.MoveNext();

				int frameCount;
				int stackCount;

				// TODO: I need to modularize this region somehow so I don't repeat so much code...
				#region Repeatition

				// We are at the end of the physical stack trace on sample on the trace, but we need to add two
				//   extra stacks for convenience and display purposes
				ProcessNode processNode;
				if (!this.ProcessNodes.TryGetValue(linuxEvent.ProcessID, out processNode))
				{
					frameCount = this.FrameCount++;
					stackCount = this.StackCount++;

					FrameInfo frameInfo = new ProcessThreadFrame(linuxEvent.ProcessID, linuxEvent.Command);

					this.IDFrame.Add(frameInfo);
					this.FrameID.Add(frameInfo.DisplayName, frameCount);

					StackNode topCaller = this.GetTopCaller(linuxEvent.ThreadID);

					processNode = new ProcessNode(stackCount, linuxEvent.ProcessID, linuxEvent.EventName, frameCount, topCaller);
					this.ProcessNodes.Add(linuxEvent.ProcessID, processNode);

					this.Stacks.Add(stackCount, processNode);
				}

				// The making of this ID might not be needed, but this is to make sure that when we look up the thread, we know
				//   it belongs to a specific process
				long processThreadID = Utils.ConcatIntegers(processNode.StackID, linuxEvent.ThreadID);

				ThreadNode threadNode;
				if (!this.ThreadNodes.TryGetValue(processThreadID, out threadNode))
				{
					frameCount = this.FrameCount++;
					stackCount = this.StackCount++;

					FrameInfo frameInfo = new ProcessThreadFrame(linuxEvent.ThreadID, "Thread");

					this.IDFrame.Add(frameInfo);
					this.FrameID.Add(frameInfo.DisplayName, frameCount);

					threadNode = new ThreadNode(stackCount, linuxEvent.ThreadID, frameCount, processNode);
					this.ThreadNodes.Add(processThreadID, threadNode);

					this.Stacks.Add(stackCount, threadNode);
				}
				#endregion;

				return threadNode; // Returns the "thread" stack for the next node to connect
			}

			StringBuilder sb = new StringBuilder();

			this.source.SkipWhiteSpace();
			this.source.ReadAsciiStringUpTo(' ', sb);
			string address = sb.ToString();
			sb.Clear();

			int frameID;

			if (!this.FrameID.TryGetValue(address, out frameID))
			{
				frameID = this.FrameCount++;
				this.FrameID.Add(address, frameID);
				this.IDFrame.Add(this.ReadFrameInfo(address));
			}
			else
			{
				// We don't care about this frame since we already have it stashed
				this.source.SkipUpTo('\n');
			}

			StackNode caller = this.DoStackTrace(linuxEvent);
			long frameStackID = Utils.ConcatIntegers(caller.StackID, frameID);

			FrameStack framestack;
			if (!FrameStacks.TryGetValue(frameStackID, out framestack))
			{
				framestack = new FrameStack(this.StackCount++, frameID, caller);
				this.FrameStacks.Add(frameStackID, framestack);
				this.Stacks.Add(framestack.StackID, framestack);
			}

			return framestack;
		}

		private FastStream source;
		private List<LinuxEvent> events;

		private StackFrame ReadFrameInfo(string address)
		{
			this.source.SkipWhiteSpace();
			var mp = this.source.MarkPosition();

			StringBuilder sb = new StringBuilder();

			this.source.ReadAsciiStringUpToLastOnLine('(', sb);
			string assumedSymbol = sb.ToString();
			sb.Clear();

			this.source.ReadAsciiStringUpTo('\n', sb);
			string assumedModule = sb.ToString();
			sb.Clear();

			assumedModule = this.RemoveOutterBrackets(assumedModule.Trim());

			string actualModule = assumedModule;
			string actualSymbol = this.RemoveOutterBrackets(assumedSymbol.Trim());

			if (assumedModule.EndsWith(".map"))
			{
				string[] moduleSymbol = this.GetModuleAndSymbol(assumedSymbol, assumedModule);
				actualModule = this.RemoveOutterBrackets(moduleSymbol[0]);
				actualSymbol = string.IsNullOrEmpty(moduleSymbol[1]) ? assumedModule : moduleSymbol[1];
			}

			if (actualModule[0] == '/' && actualModule.Length > 1)
			{
				for (int i = actualModule.Length - 1; i >= 0; i--)
				{
					if (actualModule[i] == '/')
					{
						actualModule = actualModule.Substring(i + 1);
						break;
					}
				}
			}

			return new StackFrame(address, actualModule, actualSymbol);
		}

		private string[] GetModuleAndSymbol(string assumedModule, string assumedSymbol)
		{
			string[] splits = assumedModule.Split(' ');

			for (int i = 0; i < splits.Length; i++)
			{
				string module = splits[i].Trim();
				if (module.Length > 0 && module[0] == '[' && module[module.Length - 1] == ']')
				{
					string symbol = "";
					for (int j = i + 1; j < splits.Length; j++)
					{
						symbol += splits[j] + ' ';
					}

					return new string[2] { module, symbol.Trim() };
				}
			}

			// This is suppose to safely recover if for some reason the .map sequence doesn't have a noticeable module
			return new string[2] { assumedModule, assumedSymbol };
		}

		private string RemoveOutterBrackets(string s)
		{
			if (s.Length < 1)
			{
				return s;
			}
			while ((s[0] == '(' && s[s.Length - 1] == ')')
				|| (s[0] == '[' && s[s.Length - 1] == ']'))
			{
				s = s.Substring(1, s.Length - 2);
			}

			return s;
		}

		private struct SampleInfo
		{
			internal int TopStackID { get; }
			internal double Time { get; }

			internal SampleInfo(int framestackid, double time)
			{
				this.TopStackID = framestackid;
				this.Time = time;
			}
		}

		#region FrameInfos
		private struct StackFrame : FrameInfo
		{
			internal string Address { get; }
			internal string Module { get; }
			internal string Symbol { get; }

			public string DisplayName { get { return this.Module + "!" + this.Symbol; } }

			internal StackFrame(string address, string module, string symbol)
			{
				this.Address = address;
				this.Module = module;
				this.Symbol = symbol;
			}
		}

		private struct ProcessThreadFrame : FrameInfo
		{
			internal string Name { get; }
			internal int ID { get; }
			public string DisplayName { get { return string.Format("{0} ({1})", this.Name, this.ID); } }

			internal ProcessThreadFrame(int id, string name)
			{
				this.Name = name;
				this.ID = id;
			}
		}

		private struct BlockedCPUFrame : FrameInfo
		{
			internal string Kind { get; }
			internal int ID { get; }
			public string DisplayName { get { return this.Kind; } }

			internal BlockedCPUFrame(int id, string kind)
			{
				this.ID = id;
				this.Kind = kind;
			}
		}

		private interface FrameInfo
		{
			string DisplayName { get; }
		}

		#endregion

		#region StackNodes

		private class FrameStack : StackNode
		{
			internal FrameStack(int id, int frameID, StackNode caller) :
				base(StackKind.FrameStack, id, frameID, caller)
			{
			}
		}

		private class BlockedCPUNode : StackNode
		{
			internal BlockedCPUNode(StackKind kind, int id, int frameID, StackNode caller) :
				base(kind, id, frameID, caller)
			{
			}
		}

		private class ProcessNode : StackNode
		{
			internal string Name { get; }
			internal int ID { get; }

			internal ProcessNode(int stackID, int id, string name, int frameID, StackNode invalidNode) :
				base(StackKind.Process, stackID, frameID, invalidNode)
			{
				this.Name = name;
				this.ID = id;
			}
		}

		private class ThreadNode : StackNode
		{
			/// <summary>
			/// Returns the same thing as Next but is automatically a ProcessStack
			/// instead of a StackNode
			/// </summary>
			internal ProcessNode Process { get; }

			internal int ID { get; }

			internal ThreadNode(int stackID, int id, int frameID, ProcessNode process) : base(StackKind.Thread, stackID, frameID, process)
			{
				this.Process = process;
				this.ID = id;
			}
		}

		private abstract class StackNode
		{
			internal int StackID { get; }
			internal StackKind Kind { get; }
			/// <summary>
			/// Returns the next node on the stack
			/// </summary>
			internal StackNode Caller { get; }

			internal int FrameID { get; }

			internal StackNode(StackKind kind, int id, int frameID, StackNode caller)
			{
				this.StackID = id;
				this.Kind = kind;
				this.FrameID = frameID;
				this.Caller = caller;
			}
		}

		internal enum StackKind
		{
			FrameStack,
			Process,
			Thread,
			Blocked,
			CPU,
		}

		#endregion

		private enum EventKind
		{
			General,
			Scheduled,
			CPU,
		}

		private enum ThreadState
		{
			Blocked,
			Unblocked,
		}

		private class ThreadInfo
		{

			internal int ID { get; }
			internal ThreadState State { get; private set; }

			internal double TimeStart { get; }

			private double? timeEnd;
			internal double TimeEnd
			{
				get
				{
					return timeEnd == null ? -1 : (double)timeEnd;
				}
			}
			internal double Period
			{
				get
				{
					return timeEnd == null ? -1 : this.TimeEnd - this.TimeStart;
				}
			}

			internal ThreadInfo(int threadID)
			{
				this.ID = threadID;
				this.State = ThreadState.Blocked;
			}

			internal void Unblock(double time)
			{
				this.timeEnd = time;
				this.State = ThreadState.Unblocked;
			}
		}

	}
}
