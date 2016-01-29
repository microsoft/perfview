using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClrProfiler;
using LinuxTracing.Shared;
using Validation;

namespace LinuxTracing.LinuxTraceEvent
{

	public class PerfScriptEventParser : IDisposable
	{
		public string SourceFileName { get; private set; }
		public string OutputName
		{
			get
			{
				return string.Format("{0}.perfView.xml",
					Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(this.SourcePath)));
			}
		}

		/// <summary>
		/// Gets the total number of frames parsed.
		/// </summary>
		public int FrameCount { get; private set; }

		/// <summary>
		/// Gets the total number of stacks created.
		/// </summary>
		public int StackCount { get; private set; }

		/// <summary>
		/// Gets the total number of samples created.
		/// </summary>
		public int SampleCount { get; private set; }

		/// <summary>
		/// Creates a stream reader to parse the given source file into interning stacks.
		/// </summary>
		/// <param name="pattern">Filters the samples through the event name.</param>
		/// <param name="maxSamples">Truncates the number of samples.</param>
		public void Parse(string pattern, int maxSamples, bool testing = false)
		{
			this.Initialize();

			if (testing)
			{
				this.source.MoveNext();
				this.source.MoveNext();
				this.source.MoveNext();
				this.source.MoveNext();
			}

			Regex rgx = pattern == null ? null : new Regex(pattern);
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
		/// Gets the string representation of the frame.
		/// </summary>
		/// <param name="i">The location of the frame in the array.</param>
		/// <returns>A string representing the frame in the form {module}!{symbol}.</returns>
		public string GetFrameAt(int i)
		{
			return this.IDFrame[i].DisplayName;
		}

		/// <summary>
		/// Gets the caller's ID at the given stack ID.
		/// </summary>
		/// <param name="i">The stack ID where the caller ID in question is.</param>
		/// <returns>An integer for the caller ID.</returns>
		public int GetCallerAtStack(int i)
		{
			return this.Stacks[i].Caller.StackID;
		}

		/// <summary>
		/// Gets the frame ID for the stack at the given ID.
		/// </summary>
		/// <param name="i">The ID of the stack with the frame ID in question.</param>
		/// <returns>The ID of the frame on the stack.</returns>
		public int GetFrameAtStack(int i)
		{
			return this.Stacks[i].FrameID;
		}

		/// <summary>
		/// Gets the stack at the given sample ID.
		/// </summary>
		/// <param name="i">The ID that holds the stack in question.</param>
		/// <returns>The stack ID on the sample.</returns>
		public int GetStackAtSample(int i)
		{
			return this.Samples[i].TopStackID;
		}

		/// <summary>
		/// Gets the time at the given sample ID.
		/// </summary>
		/// <param name="i">The ID that holds the time in question.</param>
		/// <returns>A double representing the time since execution in milliseconds.</returns>
		public double GetTimeInSecondsAtSample(int i)
		{
			return this.Samples[i].Time;
		}

		/// <summary>
		/// Gets the total time threads were blocked.
		/// </summary>
		/// <returns>The amount of blockage time.</returns>
		public double GetTotalBlockedTime()
		{
			double totalBlockedTime = 0;
			foreach (ThreadInfo t in this.OmittedThreads)
			{
				totalBlockedTime += t.Period;
			}

			return totalBlockedTime;
		}

		public LinuxEvent GetLinuxEventAt(int i)
		{
			return this.events[i];
		}

		public PerfScriptEventParser(string sourcePath, bool analyzeBlockedTime)
		{
			Requires.NotNull(sourcePath, nameof(sourcePath));

			this.SourcePath = sourcePath;
			this.TrackBlockedTime = analyzeBlockedTime;
		}

		#region Private

		#region Fields
		private FastStream source;
		private List<LinuxEvent> events;

		// Optimized later to only have arrays of each of these types...
		private Dictionary<string, int> FrameID;
		private List<Frame> IDFrame;
		private Dictionary<int, StackNode> Stacks;
		private Dictionary<long, FrameStack> FrameStacks;
		private List<SampleInfo> Samples;

		private Dictionary<int, ThreadInfo> BlockedThreads;
		private Dictionary<int, double> ThreadTimes;
		private List<ThreadInfo> OmittedThreads;
		private Dictionary<int, int> CPUThreadID;

		private StackNode BlockedNode { get; set; }
		private StackNode CPUNode { get; set; }

		private double CurrentTime { get; set; }
		private bool startTimeSet = false;
		private double StartTime { get; set; }
		private bool TrackBlockedTime { get; }

		private string SourcePath { get; }

		private ZipArchive Archive { get; set; }
		#endregion

		#region Important Methods
		private void Initialize()
		{
			this.source = this.GetFastStream(this.SourcePath); // new FastStream(this.SourcePath);

			this.FrameCount = 0;
			this.StackCount = 0;
			this.SampleCount = 0;

			this.FrameID = new Dictionary<string, int>();
			this.IDFrame = new List<Frame>();
			this.Stacks = new Dictionary<int, StackNode>();
			this.Stacks.Add(-1, new FrameStack(-1, -1, null));
			this.FrameStacks = new Dictionary<long, FrameStack>();
			this.Samples = new List<SampleInfo>();

			this.events = new List<LinuxEvent>();

			this.ThreadTimes = new Dictionary<int, double>();

			if (this.TrackBlockedTime)
			{
				this.BlockedThreads = new Dictionary<int, ThreadInfo>();
				this.OmittedThreads = new List<ThreadInfo>();
				this.CPUThreadID = new Dictionary<int, int>();
				this.AddCPUAndBlockedFrames();
			}
		}

		private FastStream GetFastStream(string source)
		{
			Requires.NotNull(source, nameof(source));

			FastStream stream = null;
			this.SourceFileName = Path.GetFileName(source);

			if (source.EndsWith(".zip"))
			{
				this.Archive = ZipFile.OpenRead(source);
				foreach (ZipArchiveEntry entry in this.Archive.Entries)
				{
					if (entry.FullName.EndsWith(".dump"))
					{
						stream = new FastStream(entry.Open());
						this.SourceFileName = entry.FullName;
						break;
					}
				}
			}
			else
			{
				stream = new FastStream(source);
			}

			if (stream == null) throw new InvalidProgramException("Can't find .dump in source");

			return stream;
		}

		private void AddCPUAndBlockedFrames()
		{
			int frameCount = this.FrameCount++;
			this.FrameID.Add(Constants.BLOCKED_TIME, frameCount);
			this.IDFrame.Add(new BlockedCPUFrame(frameCount, Constants.BLOCKED_TIME));

			frameCount = this.FrameCount++;
			this.FrameID.Add(Constants.CPU_TIME, frameCount);
			this.IDFrame.Add(new BlockedCPUFrame(frameCount, Constants.CPU_TIME));
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

				// Command - Stops at first number AFTER whitespace
				while (!Utils.IsNumberChar((char)this.source.Current))
				{
					sb.Append(' ');
					this.source.ReadAsciiStringUpToWhiteSpace(sb);
					this.source.SkipWhiteSpace();
				}

				string comm = sb.ToString().Trim();
				if (comm.Length > 0 && comm[0] == 0)
				{
					comm = comm.Substring(1, comm.Length - 1);
				}
				sb.Clear();

				// Process ID
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
				if (!this.startTimeSet)
				{
					this.startTimeSet = true;
					this.StartTime = time;
				}
				this.CurrentTime = time - this.StartTime;
				time = this.CurrentTime;

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
				string eventDetails = sb.ToString().Trim();
				sb.Clear();

				ScheduleSwitch scheduleSwitch = null;

				if (this.TrackBlockedTime)
				{

					if (eventDetails.Length >= ScheduledEvent.Name.Length &&
					eventDetails.Substring(0, ScheduledEvent.Name.Length) == ScheduledEvent.Name)
					{
						// Since it's a schedule switch, it's easier to read the details through the stream rather than through
						//   string manipulation, so I move back
						this.source.RestoreToMark(markedPosition);

						eventKind = EventKind.Scheduled;
						scheduleSwitch = this.ReadScheduleSwitch();

						this.source.SkipUpTo('\n');

						// The processor for this thing is not being used now...

						this.CPUThreadID[cpu] = tid;
						this.AnalyzeBlockedTime(scheduleSwitch, time);
					}
					else
					{
						// Check if CPU was being used by something else
						int threadID;
						if (this.CPUThreadID.TryGetValue(cpu, out threadID) && threadID != -1 && threadID != tid)
						{
							ThreadInfo threadInfo;
							if (this.BlockedThreads.TryGetValue(threadID, out threadInfo))
							{
								threadInfo.Unblock(time);
								this.OmitUnblockedThread(threadInfo.ID);
							}

							this.CPUThreadID[cpu] = tid;
						}
					}
				}

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

					Frame threadTimeFrame = null;

					if (this.TrackBlockedTime)
					{
						ThreadInfo ti;
						if (this.BlockedThreads.TryGetValue(tid, out ti))
						{
							threadTimeFrame = new BlockedCPUFrame(tid, Constants.BLOCKED_TIME);
						}
						else
						{
							threadTimeFrame = new BlockedCPUFrame(tid, Constants.CPU_TIME);
						}
					}

					IEnumerable<Frame> frames = this.ReadFramesForSample(comm, tid, threadTimeFrame).ToList();

					switch (eventKind)
					{
						case EventKind.Scheduled:
							{
								linuxEvent =
									new ScheduledEvent(comm, tid, pid, time, period, timeProp, cpu,
									eventName, eventDetails, frames, scheduleSwitch);
								break;
							}
						default:
							{
								linuxEvent =
									new LinuxEvent(comm, tid, pid, time, period, timeProp, cpu,
									eventName, eventDetails, frames);
								break;
							}
					}

					yield return linuxEvent;
				}
			}
		}

		private void AnalyzeBlockedTime(ScheduleSwitch scheduleSwitch, double time)
		{
			// Check if a thread has been unblocked. If a thread has been unblocked but we
			//   haven't seen it blocked, we'll just skip it and count threads like it as CPU (for now) TODO
			ThreadInfo threadInfo;
			if (this.BlockedThreads.TryGetValue(scheduleSwitch.NextThreadID, out threadInfo))
			{
				threadInfo.Unblock(time);
				this.OmitUnblockedThread(threadInfo.ID);
			}

			// Check if a thread has not been blocked and is suppose to be, otherwise if a thread is "double" blocked
			//   we just throw it away
			if (!this.BlockedThreads.TryGetValue(scheduleSwitch.PreviousThreadID, out threadInfo))
			{
				this.BlockedThreads.Add(scheduleSwitch.PreviousThreadID, new ThreadInfo(scheduleSwitch.PreviousThreadID, time));
			}


		}

		private void OmitUnblockedThread(int threadID)
		{
			// If for some reason the thread ID is not in the dictionary, then something clearly went wrong
			//   so I'll let this method throw an exception
			this.OmittedThreads.Add(this.BlockedThreads[threadID]);
			this.BlockedThreads.Remove(threadID);
		}

		private IEnumerable<Frame> ReadFramesForSample(string command, int threadID, Frame threadTimeFrame)
		{
			Func<byte, byte, bool> isEndOfStackTrace = delegate (byte current, byte peek1)
			{
				return ((current == '\n' && (peek1 == '\n' || peek1 == '\r' || peek1 == '\0')) ||
						 this.source.EndOfStream);
			};

			if (threadTimeFrame != null)
			{
				yield return threadTimeFrame;
			}

			while (!isEndOfStackTrace(this.source.Current, this.source.Peek(1)))
			{
				yield return this.ReadFrame();
			}

			yield return new ThreadFrame(threadID, "Thread");
			yield return new ProcessFrame(command);

		}
		#endregion

		#region Helpers
		private StackNode GetDeepestCaller(int threadID)
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

		private void FlushBlockedThreads()
		{
			if (!this.TrackBlockedTime) return;

			List<int> keys = this.BlockedThreads.Keys.ToList();
			foreach (int key in keys)
			{
				ThreadInfo threadInfo = this.BlockedThreads[key];
				threadInfo.Unblock(this.CurrentTime);
				this.OmitUnblockedThread(threadInfo.ID);
			}

			this.OmittedThreads.Sort(new Comparison<ThreadInfo>(
				delegate (ThreadInfo t1, ThreadInfo t2)
				{
					if (t1.TimeStart > t2.TimeStart)
					{
						return 1;
					}
					else if (t1.TimeStart < t2.TimeStart)
					{
						return -1;
					}

					return 0;
				}));

			return;
		}

		private StackFrame ReadFrame()
		{
			StringBuilder sb = new StringBuilder();

			// Address
			this.source.SkipWhiteSpace();
			this.source.ReadAsciiStringUpTo(' ', sb);
			string address = sb.ToString();
			sb.Clear();

			// Trying to get the module and symbol...
			this.source.SkipWhiteSpace();
			var mp = this.source.MarkPosition();

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

			actualModule = Path.GetFileName(actualModule);

			return new StackFrame(address, actualModule, actualSymbol);
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
		#endregion

		#region Classes
		private enum EventKind
		{
			General,
			Scheduled,
			CPU,
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

		#region BlockTime
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

			internal ThreadInfo(int threadID, double timeStart)
			{
				this.ID = threadID;
				this.TimeStart = timeStart;
				this.State = ThreadState.Blocked;
			}

			internal void Unblock(double time)
			{
				this.timeEnd = time;
				this.State = ThreadState.Unblocked;
			}
		}
		#endregion
		#endregion
		#endregion

		public void Dispose()
		{
			this.Archive?.Dispose();
		}

	}
}
