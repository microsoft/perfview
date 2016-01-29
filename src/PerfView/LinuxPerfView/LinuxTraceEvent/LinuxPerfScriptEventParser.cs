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

	public class LinuxPerfScriptEventParser : IDisposable
	{
		/// <summary>
		/// Gets the total number of samples created.
		/// </summary>
		public int EventCount { get; private set; }

		/// <summary>
		/// Creates a stream reader to parse the given source file into interning stacks.
		/// </summary>
		/// <param name="pattern">Filters the samples through the event name.</param>
		/// <param name="maxSamples">Truncates the number of samples.</param>
		public void Parse(string pattern, int maxSamples, bool testing = false)
		{
			this.events = new List<LinuxEvent>();

			if (testing)
			{
				this.Source.MoveNext();
				this.Source.MoveNext();
				this.Source.MoveNext();
				this.Source.MoveNext();
			}

			Regex rgx = pattern == null ? null : new Regex(pattern);
			foreach (LinuxEvent linuxEvent in this.NextEvent(rgx))
			{
				if (linuxEvent != null)
				{
					this.events.Add(linuxEvent);
				}

				if (this.EventCount > maxSamples)
				{
					break;
				}
			}
		}

		/// <summary>
		/// Gets the time at the given sample ID.
		/// </summary>
		/// <param name="i">The ID that holds the time in question.</param>
		/// <returns>A double representing the time since execution in milliseconds.</returns>
		public double GetTimeInSecondsAtEvent(int i)
		{
			return this.events[i].Time;
		}

		public LinuxEvent GetLinuxEventAt(int i)
		{
			return this.events[i];
		}

		public LinuxPerfScriptEventParser(string path)
		{
			Requires.NotNull(path, nameof(path));
			this.Source = new FastStream(path);
		}

		public LinuxPerfScriptEventParser(Stream stream)
		{
			Requires.NotNull(stream, nameof(stream));
			this.Source = new FastStream(stream);
		}

		#region Fields
		private FastStream Source { get; }
		private List<LinuxEvent> events;


		private double CurrentTime { get; set; }

		private bool startTimeSet = false;
		private double StartTime { get; set; }
		private ZipArchive Archive { get; set; }
		#endregion

		private IEnumerable<LinuxEvent> NextEvent(Regex regex)
		{

			string line = string.Empty;

			while (true)
			{

				this.Source.SkipWhiteSpace();

				if (this.Source.EndOfStream)
				{
					break;
				}

				EventKind eventKind = EventKind.General;

				StringBuilder sb = new StringBuilder();

				// Command - Stops at first number AFTER whitespace
				while (!Utils.IsNumberChar((char)this.Source.Current))
				{
					sb.Append(' ');
					this.Source.ReadAsciiStringUpToWhiteSpace(sb);
					this.Source.SkipWhiteSpace();
				}

				string comm = sb.ToString().Trim();
				if (comm.Length > 0 && comm[0] == 0)
				{
					comm = comm.Substring(1, comm.Length - 1);
				}
				sb.Clear();

				// Process ID
				int pid = this.Source.ReadInt();
				this.Source.MoveNext();

				// Thread ID
				int tid = this.Source.ReadInt();

				// CPU
				this.Source.SkipWhiteSpace();
				this.Source.MoveNext();
				int cpu = this.Source.ReadInt();
				this.Source.MoveNext();

				// Time
				this.Source.SkipWhiteSpace();
				this.Source.ReadAsciiStringUpTo(':', sb);
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
				this.Source.MoveNext();
				this.Source.SkipWhiteSpace();
				int timeProp = this.Source.ReadInt(); // for now we just move past it...

				// Event Name
				this.Source.SkipWhiteSpace();
				this.Source.ReadAsciiStringUpTo(':', sb);
				string eventName = sb.ToString();
				sb.Clear();
				this.Source.MoveNext();

				// Event Properties
				// I mark a position here because I need to check what type of event this is without screwing up the stream
				var markedPosition = this.Source.MarkPosition();
				this.Source.ReadAsciiStringUpTo('\n', sb);
				string eventDetails = sb.ToString().Trim();
				sb.Clear();

				ScheduleSwitch scheduleSwitch = null;

				// Now that we know the header of the trace, we can decide whether or not to skip it given our pattern
				if (regex != null && !regex.IsMatch(eventName))
				{
					while (true)
					{
						this.Source.MoveNext();
						if ((this.Source.Current == '\n' &&
							(this.Source.Peek(1) == '\n' || this.Source.Peek(1) == '\r' || this.Source.Peek(1) == 0)) ||
							 this.Source.EndOfStream)
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

					IEnumerable<Frame> frames = this.ReadFramesForSample(comm, tid, threadTimeFrame).ToList();

					switch (eventKind)
					{
						case EventKind.Scheduled:
							{
								linuxEvent =
									new ScheduledEvent(comm, tid, pid, time, timeProp, cpu,
									eventName, eventDetails, frames, scheduleSwitch);
								break;
							}
						default:
							{
								linuxEvent =
									new LinuxEvent(comm, tid, pid, time, timeProp, cpu,
									eventName, eventDetails, frames);
								break;
							}
					}

					yield return linuxEvent;
				}
			}
		}

		private IEnumerable<Frame> ReadFramesForSample(string command, int threadID, Frame threadTimeFrame)
		{
			Func<byte, byte, bool> isEndOfStackTrace = delegate (byte current, byte peek1)
			{
				return ((current == '\n' && (peek1 == '\n' || peek1 == '\r' || peek1 == '\0')) ||
						 this.Source.EndOfStream);
			};

			if (threadTimeFrame != null)
			{
				yield return threadTimeFrame;
			}

			while (!isEndOfStackTrace(this.Source.Current, this.Source.Peek(1)))
			{
				yield return this.ReadFrame();
			}

			yield return new ThreadFrame(threadID, "Thread");
			yield return new ProcessFrame(command);

		}

		private StackFrame ReadFrame()
		{
			StringBuilder sb = new StringBuilder();

			// Address
			this.Source.SkipWhiteSpace();
			this.Source.ReadAsciiStringUpTo(' ', sb);
			string address = sb.ToString();
			sb.Clear();

			// Trying to get the module and symbol...
			this.Source.SkipWhiteSpace();
			var mp = this.Source.MarkPosition();

			this.Source.ReadAsciiStringUpToLastOnLine('(', sb);
			string assumedSymbol = sb.ToString();
			sb.Clear();

			this.Source.ReadAsciiStringUpTo('\n', sb);
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

			this.Source.SkipUpTo('=');
			this.Source.MoveNext();

			this.Source.ReadAsciiStringUpTo(' ', sb);
			string prevComm = sb.ToString();
			sb.Clear();

			this.Source.SkipUpTo('=');
			this.Source.MoveNext();

			int prevTid = this.Source.ReadInt();

			this.Source.SkipUpTo('=');
			this.Source.MoveNext();

			int prevPrio = this.Source.ReadInt();

			this.Source.SkipUpTo('=');
			this.Source.MoveNext();

			char prevState = (char)this.Source.Current;

			this.Source.MoveNext();
			this.Source.SkipUpTo('n'); // this is to bypass the ==>
			this.Source.SkipUpTo('=');
			this.Source.MoveNext();

			this.Source.ReadAsciiStringUpTo(' ', sb);
			string nextComm = sb.ToString();
			sb.Clear();

			this.Source.SkipUpTo('=');
			this.Source.MoveNext();

			int nextTid = this.Source.ReadInt();

			this.Source.SkipUpTo('=');
			this.Source.MoveNext();

			int nextPrio = this.Source.ReadInt();

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

		public void Dispose()
		{
			this.Archive?.Dispose();
		}
	}

	public enum EventKind
	{
		General,
		Scheduled,
	}

	public class ScheduledEvent : LinuxEvent
	{
		public static readonly string Name = "sched_switch";

		public ScheduleSwitch Switch { get; }

		public ScheduledEvent(
			string comm, int tid, int pid,
			double time, int timeProp, int cpu,
			string eventName, string eventProp, IEnumerable<Frame> callerStacks, ScheduleSwitch schedSwitch) :
			base(comm, tid, pid, time, timeProp, cpu, eventName, eventProp, callerStacks)
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
			double time, int timeProp, int cpu,
			string eventName, string eventProp, IEnumerable<Frame> callerStacks)
		{
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

	public interface Frame
	{
		string DisplayName { get; }
	}

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

	public struct ProcessFrame : Frame
	{
		public string Name { get; }

		public string DisplayName { get { return this.Name; } }

		internal ProcessFrame(string name)
		{
			this.Name = name;
		}
	}

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
