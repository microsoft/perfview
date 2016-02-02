using System;
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
using Validation;

namespace Diagnostics.Tracing.StackSources
{

	public class LinuxPerfScriptStackSource : InternStackSource
	{

		public static readonly string PerfScriptSuffix = "perf.data.dump";

		private LinuxPerfScriptEventParser Parser { get; }

		public LinuxPerfScriptStackSource(LinuxPerfScriptEventParser parser)
		{
			if (!parser.Parsed)
			{
				parser.Parse();
			}

			this.Parser = parser;
			this.InternAllLinuxEvents();
		}

		public LinuxPerfScriptStackSource(string path)
		{
			Stream stream = null;

			if (path.EndsWith(".zip"))
			{
				ZipArchive archive = new ZipArchive(new FileStream(path, FileMode.Open));
				ZipArchiveEntry foundEntry = null;
				foreach (ZipArchiveEntry entry in archive.Entries)
				{
					if (entry.FullName.EndsWith(PerfScriptSuffix))
					{
						foundEntry = entry;
						break;
					}
				}

				stream = foundEntry.Open();
			}
			else
			{
				if (path.EndsWith(PerfScriptSuffix))
				{
					stream = new FileStream(path, FileMode.Open);
				}
				else
				{
					throw new Exception("Not a valid input file");
				}
			}

			if (stream != null)
			{
				this.Parser = new LinuxPerfScriptEventParser(stream);
			}
			else
			{
				throw new Exception(".zip does not contain a perf.data.dump file suffix entry");
			}

			this.InternAllLinuxEvents();
		}

		#region private
		private void InternAllLinuxEvents()
		{
			StackSourceCallStackIndex stackIndex = 0;
			foreach (LinuxEvent linuxEvent in this.Parser.Parse())
			{
				IEnumerable<Frame> frames = linuxEvent.CallerStacks;

				stackIndex = this.InternFrames(frames.GetEnumerator(), stackIndex);

				var sample = new StackSourceSample(this);
				sample.StackIndex = stackIndex;
				sample.TimeRelativeMSec = linuxEvent.Time;
				sample.Metric = 1;
				this.AddSample(sample);
			}

			this.Interner.DoneInterning();
		}

		private StackSourceCallStackIndex InternFrames(
			IEnumerator<Frame> frameIterator, StackSourceCallStackIndex stackIndex)
		{
			if (!frameIterator.MoveNext())
			{
				return StackSourceCallStackIndex.Invalid;
			}

			var frameIndex = this.Interner.FrameIntern(frameIterator.Current.DisplayName);
			stackIndex = this.Interner.CallStackIntern(frameIndex, this.InternFrames(frameIterator, stackIndex));
			return stackIndex;
		}

		#endregion
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

		/// <summary>
		/// Returns true if parser is in testing mode, false otherwise.
		/// (Testing mode skips BOM with VS txt files)
		/// </summary>
		public bool Testing { get; set; }

		/// <summary>
		/// Creates a stream reader to parse the given source file into interning stacks.
		/// </summary>
		public IEnumerable<LinuxEvent> Parse()
		{
			this.Source.MoveNext(); // Skip Sentinal value
			
			if (this.Testing)
			{
				for (int i = 0; i < 3; i++)
				{
					this.Source.MoveNext();
				}
			}

			Regex rgx = this.Pattern;
			foreach (LinuxEvent linuxEvent in this.NextEvent(rgx))
			{
				if (linuxEvent != null)
				{
					this.EventCount++;
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

		public Regex Pattern { get; set; }
		public long MaxSamples { get; set; }

		public LinuxPerfScriptEventParser(string path)
		{
			Requires.NotNull(path, nameof(path));
			this.Source = new FastStream(path);
			this.SetDefaultValues();
		}

		public LinuxPerfScriptEventParser(Stream stream)
		{
			Requires.NotNull(stream, nameof(stream));
			this.Source = new FastStream(stream);
			this.SetDefaultValues();
		}

		#region fields
		private FastStream Source { get; }

		private double CurrentTime { get; set; }

		private bool startTimeSet = false;
		private double StartTime { get; set; }
		#endregion

		private void SetDefaultValues()
		{
			this.EventCount = 0;
			this.Testing = false;
			this.Parsed = false;
			this.Pattern = null;
			this.MaxSamples = 50000;
		}

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

				StringBuilder sb = new StringBuilder();

				// Command - Stops at first number AFTER whitespace
				while (!this.IsNumberChar((char)this.Source.Current))
				{
					sb.Append(' ');
					this.Source.ReadAsciiStringUpToWhiteSpace(sb);
					this.Source.SkipWhiteSpace();
				}

				string comm = sb.ToString().Trim();
				sb.Clear();

				// Process ID
				int pid = this.Source.ReadInt();
				this.Source.MoveNext(); // Move past the "/"

				// Thread ID
				int tid = this.Source.ReadInt();

				// CPU
				this.Source.SkipWhiteSpace();
				this.Source.MoveNext(); // Move past the "["
				int cpu = this.Source.ReadInt();
				this.Source.MoveNext(); // Move past the "]"

				// Time
				this.Source.SkipWhiteSpace();
				this.Source.ReadAsciiStringUpTo(':', sb);

				double time = double.Parse(sb.ToString());

				if (time == 411965.212798)
				{
					Console.WriteLine("something");
				}

				sb.Clear();
				if (!this.startTimeSet)
				{
					this.startTimeSet = true;
					this.StartTime = time;
				}
				this.CurrentTime = time - this.StartTime;
				time = this.CurrentTime;
				this.Source.MoveNext(); // Move past ":"

				// Time Property
				this.Source.SkipWhiteSpace();
				int timeProp = -1;
				if (this.IsNumberChar((char)this.Source.Current))
				{
					timeProp = this.Source.ReadInt();
				}

				// Event Name
				this.Source.SkipWhiteSpace();
				this.Source.ReadAsciiStringUpTo(':', sb);
				string eventName = sb.ToString();
				sb.Clear();
				this.Source.MoveNext();

				// Event Properties
				// I mark a position here because I need to check what type of event this is without screwing up the stream
				// var markedPosition = this.Source.MarkPosition();
				this.Source.ReadAsciiStringUpTo('\n', sb);
				string eventDetails = sb.ToString().Trim();
				sb.Clear();

				// Now that we know the header of the trace, we can decide whether or not to skip it given our pattern
				if (regex != null && !regex.IsMatch(eventName))
				{
					while (true)
					{
						this.Source.MoveNext();
						if (this.IsEndOfSample())
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

					IEnumerable<Frame> frames = this.ReadFramesForSample(comm, tid, threadTimeFrame);

					linuxEvent = new LinuxEvent(comm, tid, pid, time, timeProp, cpu, eventName, eventDetails, frames);

					yield return linuxEvent;
				}
			}
		}

		private bool IsEndOfSample()
		{
			byte current = this.Source.Current;
			byte peek1 = this.Source.Peek(1);
			return (current == '\n' && (peek1 == '\n' || peek1 == '\r' || peek1 == 0)) || this.Source.EndOfStream;
		}

		private List<Frame> ReadFramesForSample(string command, int threadID, Frame threadTimeFrame)
		{
			List<Frame> frames = new List<Frame>();

			if (threadTimeFrame != null)
			{
				frames.Add(threadTimeFrame);
			}

			while (!this.IsEndOfSample())
			{
				frames.Add(this.ReadFrame());
			}

			frames.Add(new ThreadFrame(threadID, "Thread"));
			frames.Add(new ProcessFrame(command));

			return frames;
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
