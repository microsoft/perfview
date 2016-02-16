// using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClrProfiler;
using Microsoft.Diagnostics.Tracing.Stacks;
using PerfView.Utilities;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace Diagnostics.Tracing.StackSources
{

	public class LinuxPerfScriptStackSource : InternStackSource
	{
		public static readonly string[] PerfDumpSuffixes = new string[]
		{
			".data.dump", ".data.txt", ".trace.zip"
		};

		public LinuxPerfScriptStackSource(string path, bool doThreadTime = false)
		{

			ZipArchive archive = null;
			using (Stream stream = this.GetPerfScriptStream(path, out archive))
			{
				this.parser = new LinuxPerfScriptEventParser(stream);
				this.InternAllLinuxEvents(doThreadTime);
				stream.Close();
			}
			archive?.Dispose();
		}

		public double GetTotalBlockedTime()
		{
			Contract.Requires(this.threadBlockedPeriods != null, nameof(threadBlockedPeriods));
			double timeBlocked = 0;
			foreach (ThreadPeriod period in this.threadBlockedPeriods)
			{
				timeBlocked += period.Period;
			}

			return timeBlocked;
		}

		#region private

		private readonly LinuxPerfScriptEventParser parser;

		private Dictionary<int, double> blockedThreads;
		private List<ThreadPeriod> threadBlockedPeriods;
		private Dictionary<int, int> cpuThreadUsage;

		private enum StateThread
		{
			BLOCKED_TIME,
			CPU_TIME
		}

		private class ThreadPeriod
		{
			internal double StartTime { get; }
			internal double EndTime { get; }
			internal double Period { get { return this.EndTime - this.StartTime; } }

			internal ThreadPeriod(double startTime, double endTime)
			{
				this.StartTime = startTime;
				this.EndTime = endTime;
			}
		}

		private void InternAllLinuxEvents(bool doThreadTime)
		{
			if (doThreadTime)
			{
				this.blockedThreads = new Dictionary<int, double>();
				this.threadBlockedPeriods = new List<ThreadPeriod>();
				this.cpuThreadUsage = new Dictionary<int, int>();
			}

			double lastTime = 0;

			StackSourceCallStackIndex stackIndex = 0;
			foreach (LinuxEvent linuxEvent in this.parser.Parse())
			{
				lastTime = linuxEvent.Time;

				if (doThreadTime)
				{
					this.AnalyzeSampleForBlockedTime(linuxEvent);
				}

				IEnumerable<Frame> frames = linuxEvent.CallerStacks;

				stackIndex = this.InternFrames(frames.GetEnumerator(), stackIndex, linuxEvent.ThreadID, doThreadTime);

				var sample = new StackSourceSample(this);
				sample.StackIndex = stackIndex;
				sample.TimeRelativeMSec = linuxEvent.Time;
				sample.Metric = 1;
				this.AddSample(sample);
			}

			if (doThreadTime)
			{
				this.FlushBlockedThreadsAt(lastTime);
				this.threadBlockedPeriods.Sort((x, y) => x.StartTime.CompareTo(y.StartTime));
			}

			this.Interner.DoneInterning();
		}

		private void FlushBlockedThreadsAt(double endTime)
		{
			foreach (int threadid in this.blockedThreads.Keys)
			{
				double startTime = this.blockedThreads[threadid];
				this.threadBlockedPeriods.Add(new ThreadPeriod(startTime, endTime));
			}

			this.blockedThreads = null;
		}

		private void AnalyzeSampleForBlockedTime(LinuxEvent linuxEvent)
		{
			// This is check for completed scheduler events, ones that start with prev_comm and have 
			//   corresponding next_comm.
			if (linuxEvent.Kind == EventKind.Scheduler)
			{
				SchedulerEvent schedEvent = (SchedulerEvent)linuxEvent;
				if (!this.blockedThreads.ContainsKey(schedEvent.Switch.PreviousThreadID))
				{
					this.blockedThreads.Add(schedEvent.Switch.PreviousThreadID, schedEvent.Time);
				}

				double startTime;
				if (this.blockedThreads.TryGetValue(schedEvent.Switch.NextThreadID, out startTime))
				{
					this.blockedThreads.Remove(schedEvent.Switch.NextThreadID);
					this.threadBlockedPeriods.Add(new ThreadPeriod(startTime, schedEvent.Time));
				}

			}
			// This is for induced blocked time, if the thread that has already been blocked is
			//   somehow now unblocked but we didn't get a scheduled event for it.
			else if (linuxEvent.Kind == EventKind.Cpu)
			{
				int threadid;
				if (this.cpuThreadUsage.TryGetValue(linuxEvent.Cpu, out threadid) && threadid != linuxEvent.ThreadID)
				{
					double startTime;
					if (this.blockedThreads.TryGetValue(threadid, out startTime))
					{
						this.blockedThreads.Remove(threadid);
						this.threadBlockedPeriods.Add(new ThreadPeriod(startTime, linuxEvent.Time));
					}
				}
			}

			this.cpuThreadUsage[linuxEvent.Cpu] = linuxEvent.ThreadID;
		}

		private StackSourceCallStackIndex InternFrames(IEnumerator<Frame> frameIterator, StackSourceCallStackIndex stackIndex, int? threadid = null, bool doThreadTime = false)
		{

			// We shouldn't advance the iterator if thread time is enabled because we need 
			//   to add an extra frame to the caller stack that is not in the frameIterator.
			//   i.e. Short-circuiting prevents the frameIterator from doing MoveNext :)
			if (!doThreadTime && !frameIterator.MoveNext())
			{
				return StackSourceCallStackIndex.Invalid;
			}

			StackSourceFrameIndex frameIndex;
			if (doThreadTime)
			{
				// If doThreadTime is true, then we need to make sure that threadid is not null
				Contract.Requires(threadid != null, nameof(threadid));

				if (this.blockedThreads.ContainsKey((int)threadid))
				{
					frameIndex = this.Interner.FrameIntern(StateThread.BLOCKED_TIME.ToString());
				}
				else
				{
					frameIndex = this.Interner.FrameIntern(StateThread.CPU_TIME.ToString());
				}
			}
			else
			{
				frameIndex = this.Interner.FrameIntern(frameIterator.Current.DisplayName);
			}


			stackIndex = this.Interner.CallStackIntern(frameIndex, this.InternFrames(frameIterator, stackIndex));
			return stackIndex;
		}

		private Stream GetPerfScriptStream(string path, out ZipArchive archive)
		{
			archive = null;
			if (path.EndsWith(".zip"))
			{
				archive = new ZipArchive(new FileStream(path, FileMode.Open));
				ZipArchiveEntry foundEntry = null;
				foreach (ZipArchiveEntry entry in archive.Entries)
				{
					if (entry.FullName.EndsWithOneOf(PerfDumpSuffixes))
					{
						foundEntry = entry;
						break;
					}
				}
				return foundEntry.Open();
			}
			else
			{
				if (path.EndsWithOneOf(PerfDumpSuffixes))
				{
					return new FileStream(path, FileMode.Open);
				}
			}

			throw new System.Exception("Not a valid input file");
		}
		#endregion
	}

	public static class StringExtension
	{
		internal static bool EndsWithOneOf(this string path, string[] suffixes)
		{
			foreach (string suffix in suffixes)
			{
				if (path.EndsWith(suffix))
				{
					return true;
				}
			}

			return false;
		}
	}

	public class LinuxPerfScriptEventParser
	{
		/// <summary>
		/// Gets the total number of samples created.
		/// </summary>
		public int EventCount { get; private set; }

		/// <summary>
		/// True if the source given had been parsed before, otherwise false.
		/// </summary>
		public bool Parsed { get; private set; }


		// Basically, if the length returned is -1, then there's no more stream in the
		//   master source, otherwise, buffer should be valid with the length returned 
		private int RequestBuffer(byte[] buffer)
		{
			throw new System.NotImplementedException();
		}

		/// <summary>
		/// Parses the PerfScript .dump file given, gives one sample at a time
		/// </summary>
		public IEnumerable<LinuxEvent> Parse()
		{
			this.MasterSource.MoveNext(); // Skip Sentinal value

			while (Encoding.UTF8.GetPreamble().Contains(this.MasterSource.Current)) // Skip the BOM marks if there are any
			{
				this.MasterSource.MoveNext();
			}

			this.MasterSource.SkipWhiteSpace(); // Make sure we start at the beginning of a sample.



			while (!this.MasterSource.EndOfStream)
			{
				int start = (int)this.MasterSource.BufferIndex;
				int length = this.MasterSource.Buffer.Length - start;
				length = this.GetCompleteBuffer(start, length);

				FastStream source1 = new FastStream(this.MasterSource.Buffer, start, length);

				// Create the tasks other FastStreams, we have the master FastStream Source which
				//   basically initializes the position of the buffer and stuff

				foreach (LinuxEvent linuxEvent in this.ParseBoundedBuffer(source1))
				{
					yield return linuxEvent;
				}

				// Wait until all the tasks are finished

				// We need to refill the buffer as much as we can
				this.MasterSource.FillBufferFromStreamPosition(keepLast: (this.MasterSource.BufferFillPosition - (uint)start) - (uint)length);

				// This is to ensure that we start at the beginning of sample next loop and also so that we hit
				//   the end of the stream if there's nothing left.
				this.MasterSource.SkipWhiteSpace();
			}
		}

		private int GetCompleteBuffer(int index, int count, double estimatedCountPortion = 0.8)
		{
			if (this.MasterSource.BufferFillPosition - index < count)
			{
				return (int)this.MasterSource.BufferFillPosition - index;
			}
			else
			{
				int newCount = (int)(this.MasterSource.BufferFillPosition * estimatedCountPortion);

				for (int i = index + newCount; i < this.MasterSource.BufferFillPosition - 1; i++)
				{
					int bytesAhead = (int)(i - this.MasterSource.BufferIndex);
					newCount++;
					if (this.IsEndOfSample(this.MasterSource,
						this.MasterSource.Peek(bytesAhead),
						this.MasterSource.Peek(bytesAhead + 1)))
					{
						break;
					}

					if (i == this.MasterSource.BufferFillPosition - 2)
					{
						// This is just in case we don't find an end to the stack we're on... In that case we need
						//   to make the estimatedCountPortion smaller to capture more stuff
						return this.GetCompleteBuffer(index, count, estimatedCountPortion * 0.9);
					}
				}

				return newCount;
			}
		}

		private IEnumerable<LinuxEvent> ParseBoundedBuffer(FastStream source)
		{
			Regex rgx = this.Pattern;
			foreach (LinuxEvent linuxEvent in this.NextEvent(rgx, source))
			{
				if (linuxEvent != null)
				{
					this.EventCount++; // Needs to be thread safe
					yield return linuxEvent;
				}

				if (this.EventCount > this.MaxSamples)
				{
					break;
				}
			}

			this.Parsed = true;
			yield break;
		}

		/// <summary>
		/// Regex string pattern for filtering events.
		/// </summary>
		public Regex Pattern { get; set; }

		/// <summary>
		/// The amount of samples the parser takes.
		/// </summary>
		public long MaxSamples { get; set; }

		public LinuxPerfScriptEventParser(string path) :
			this(new FileStream(path, FileMode.Open))
		{
		}

		public LinuxPerfScriptEventParser(Stream stream)
		{
			Contract.Requires(stream != null, nameof(stream));
			this.MasterSource = new FastStream(stream);
			this.SetDefaultValues();
		}

		#region fields
		private FastStream MasterSource { get; }
		private double CurrentTime { get; set; }
		private bool startTimeSet = false;
		private double StartTime { get; set; }
		#endregion

		private void SetDefaultValues()
		{
			this.EventCount = 0;
			this.Parsed = false;
			this.Pattern = null;
			this.MaxSamples = 50000;
		}

		private IEnumerable<LinuxEvent> NextEvent(Regex regex, FastStream source)
		{

			string line = string.Empty;

			// Skip the 0
			source.MoveNext();

			while (true)
			{

				// This is to make sure we don't leave the Buffer and accidently write to it!
				source.SkipUpToFalse(delegate (byte c)
				{
					if (!source.EndOfStream && char.IsWhiteSpace((char)c))
					{
						return true;
					}

					return false;
				});

				if (source.EndOfStream)
				{
					break;
				}

				EventKind eventKind = EventKind.Cpu;

				StringBuilder sb = new StringBuilder();

				// Command - Stops at first number AFTER whitespace
				while (!this.IsNumberChar((char)source.Current))
				{
					sb.Append(' ');
					source.ReadAsciiStringUpToTrue(sb, delegate (byte c)
					{
						return !char.IsWhiteSpace((char)c);
					});
					source.SkipWhiteSpace();
				}

				string comm = sb.ToString().Trim();
				sb.Clear();

				// Process ID
				int pid = source.ReadInt();
				source.MoveNext(); // Move past the "/"

				// Thread ID
				int tid = source.ReadInt();

				// CPU
				source.SkipWhiteSpace();
				source.MoveNext(); // Move past the "["
				int cpu = source.ReadInt();
				source.MoveNext(); // Move past the "]"

				// Time
				source.SkipWhiteSpace();
				source.ReadAsciiStringUpTo(':', sb);

				double time = double.Parse(sb.ToString());

				sb.Clear();
				if (!this.startTimeSet)
				{
					this.startTimeSet = true;
					this.StartTime = time;
				}
				this.CurrentTime = time - this.StartTime;
				time = this.CurrentTime;
				source.MoveNext(); // Move past ":"

				// Time Property
				source.SkipWhiteSpace();
				int timeProp = -1;
				if (this.IsNumberChar((char)source.Current))
				{
					timeProp = source.ReadInt();
				}

				// Event Name
				source.SkipWhiteSpace();
				source.ReadAsciiStringUpTo(':', sb);
				string eventName = sb.ToString();
				sb.Clear();
				source.MoveNext();

				// Event Properties
				// I mark a position here because I need to check what type of event this is without screwing up the stream
				var markedPosition = source.MarkPosition();
				source.ReadAsciiStringUpTo('\n', sb);
				string eventDetails = sb.ToString().Trim();
				sb.Clear();

				if (eventDetails.Length >= SchedulerEvent.Name.Length && eventDetails.Substring(0, SchedulerEvent.Name.Length) == SchedulerEvent.Name)
				{
					eventKind = EventKind.Scheduler;
				}

				// Now that we know the header of the trace, we can decide whether or not to skip it given our pattern
				if (regex != null && !regex.IsMatch(eventName))
				{
					while (true)
					{
						source.MoveNext();
						if (this.IsEndOfSample(source, source.Current, source.Peek(1)))
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

					// For the sake of immutability, I have to do a similar if-statement twice. I'm trying to figure out a better way
					//   but for now this will do.
					ScheduleSwitch schedSwitch = null;
					if (eventKind == EventKind.Scheduler)
					{
						source.RestoreToMark(markedPosition);
						schedSwitch = this.ReadScheduleSwitch(source);
						source.SkipUpTo('\n');
					}

					IEnumerable<Frame> frames = this.ReadFramesForSample(comm, tid, threadTimeFrame, source);

					if (eventKind == EventKind.Scheduler)
					{
						linuxEvent = new SchedulerEvent(comm, tid, pid, time, timeProp, cpu, eventName, eventDetails, frames, schedSwitch);
					}
					else
					{
						linuxEvent = new CpuEvent(comm, tid, pid, time, timeProp, cpu, eventName, eventDetails, frames);
					}

					yield return linuxEvent;
				}
			}
		}

		private bool IsEndOfSample(FastStream source, byte current, byte peek1)
		{
			return (current == '\n' && (peek1 == '\n' || peek1 == '\r' || peek1 == 0)) || source.EndOfStream;
		}

		private List<Frame> ReadFramesForSample(string command, int threadID, Frame threadTimeFrame, FastStream source)
		{
			List<Frame> frames = new List<Frame>();

			if (threadTimeFrame != null)
			{
				frames.Add(threadTimeFrame);
			}

			while (!this.IsEndOfSample(source, source.Current, source.Peek(1)))
			{
				frames.Add(this.ReadFrame(source));
			}

			frames.Add(new ThreadFrame(threadID, "Thread"));
			frames.Add(new ProcessFrame(command));

			return frames;
		}

		private StackFrame ReadFrame(FastStream source)
		{
			StringBuilder sb = new StringBuilder();

			// Address
			source.SkipWhiteSpace();
			source.ReadAsciiStringUpTo(' ', sb);
			string address = sb.ToString();
			sb.Clear();

			// Trying to get the module and symbol...
			source.SkipWhiteSpace();

			source.ReadAsciiStringUpToLastBeforeTrue('(', sb, delegate (byte c)
			{
				if (c != '\n' && !source.EndOfStream)
				{
					return true;
				}

				return false;
			});
			string assumedSymbol = sb.ToString();
			sb.Clear();

			source.ReadAsciiStringUpTo('\n', sb);

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

		private ScheduleSwitch ReadScheduleSwitch(FastStream source)
		{
			StringBuilder sb = new StringBuilder();

			source.SkipUpTo('=');
			source.MoveNext();

			source.ReadAsciiStringUpTo(' ', sb);
			string prevComm = sb.ToString();
			sb.Clear();

			source.SkipUpTo('=');
			source.MoveNext();

			int prevTid = source.ReadInt();

			source.SkipUpTo('=');
			source.MoveNext();

			int prevPrio = source.ReadInt();

			source.SkipUpTo('=');
			source.MoveNext();

			char prevState = (char)source.Current;

			source.MoveNext();
			source.SkipUpTo('n'); // this is to bypass the ==>
			source.SkipUpTo('=');
			source.MoveNext();

			source.ReadAsciiStringUpTo(' ', sb);
			string nextComm = sb.ToString();
			sb.Clear();

			source.SkipUpTo('=');
			source.MoveNext();

			int nextTid = source.ReadInt();

			source.SkipUpTo('=');
			source.MoveNext();

			int nextPrio = source.ReadInt();

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

		private bool IsNumberChar(char c)
		{
			switch (c)
			{
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
				case '0':
					return true;
			}

			return false;
		}
	}

	/// <summary>
	/// Defines the kind of an event for easy casting.
	/// </summary>
	public enum EventKind
	{
		Cpu,
		Scheduler,
	}

	/// <summary>
	/// A sample that has extra properties to hold scheduled events.
	/// </summary>
	public class SchedulerEvent : LinuxEvent
	{
		public static readonly string Name = "sched_switch";

		/// <summary>
		/// The details of the context switch.
		/// </summary>
		public ScheduleSwitch Switch { get; }

		public SchedulerEvent(
			string comm, int tid, int pid,
			double time, int timeProp, int cpu,
			string eventName, string eventProp, IEnumerable<Frame> callerStacks, ScheduleSwitch schedSwitch) :
			base(EventKind.Scheduler, comm, tid, pid, time, timeProp, cpu, eventName, eventProp, callerStacks)
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

	public class CpuEvent : LinuxEvent
	{
		public CpuEvent(
			string comm, int tid, int pid,
			double time, int timeProp, int cpu,
			string eventName, string eventProp, IEnumerable<Frame> callerStacks) :
			base(EventKind.Cpu, comm, tid, pid, time, timeProp, cpu, eventName, eventProp, callerStacks)
		{ }
	}

	/// <summary>
	/// A generic Linux event, all Linux events contain these properties.
	/// </summary>
	public abstract class LinuxEvent
	{
		public EventKind Kind { get; }
		public string Command { get; }
		public int ThreadID { get; }
		public int ProcessID { get; }
		public double Time { get; }
		public int TimeProperty { get; }
		public int Cpu { get; }
		public string EventName { get; }
		public string EventProperty { get; }
		public IEnumerable<Frame> CallerStacks { get; }

		public LinuxEvent(EventKind kind,
			string comm, int tid, int pid,
			double time, int timeProp, int cpu,
			string eventName, string eventProp, IEnumerable<Frame> callerStacks)
		{
			this.Kind = kind;
			this.Command = comm;
			this.ThreadID = tid;
			this.ProcessID = pid;
			this.Time = time;
			this.TimeProperty = timeProp;
			this.Cpu = cpu;
			this.EventName = eventName;
			this.EventProperty = eventProp;
			this.CallerStacks = callerStacks;
		}
	}

	/// <summary>
	/// A way to define different types of frames with different names on PerfView.
	/// </summary>
	public interface Frame
	{
		string DisplayName { get; }
	}

	/// <summary>
	/// Defines a single stack frame on a linux sample.
	/// </summary>
	public struct StackFrame : Frame
	{
		public string Address { get; }
		public string Module { get; }
		public string Symbol { get; }

		public string DisplayName { get { return string.Format("{0}!{1}", this.Module, this.Symbol); } }

		internal StackFrame(string address, string module, string symbol)
		{
			this.Address = address;
			this.Module = module;
			this.Symbol = symbol;
		}
	}

	/// <summary>
	/// Represents the name of the process.
	/// </summary>
	public struct ProcessFrame : Frame
	{
		public string Name { get; }

		public string DisplayName { get { return this.Name; } }

		internal ProcessFrame(string name)
		{
			this.Name = name;
		}
	}

	/// <summary>
	/// Represents the name of the thread and its ID.
	/// </summary>
	public struct ThreadFrame : Frame
	{
		public string Name { get; }
		public int ID { get; }
		public string DisplayName { get { return string.Format("{0} ({1})", this.Name, this.ID); } }

		internal ThreadFrame(int id, string name)
		{
			this.Name = name;
			this.ID = id;
		}
	}

	/// <summary>
	/// A visual frame that represents whether or not a call stack was blocked or not.
	/// </summary>
	public struct BlockedCPUFrame : Frame
	{
		public string Kind { get; }
		public int ID { get; }
		public string DisplayName { get { return this.Kind; } }

		internal BlockedCPUFrame(int id, string kind)
		{
			this.ID = id;
			this.Kind = kind;
		}
	}
}
