using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Stacks;
using PerfView.Utilities;

namespace Diagnostics.Tracing.StackSources
{
	public class LinuxPerfScriptEventParser
	{
		public LinuxPerfScriptEventParser()
		{
			this.mapper = null;
			this.SetDefaultValues();
		}

		/// <summary>
		/// Gets the total number of samples created.
		/// </summary>
		public int EventCount { get; private set; }

		/// <summary>
		/// True if the source given had been parsed before, otherwise false.
		/// </summary>
		public bool Parsed { get; private set; }

		public void SkipPreamble(FastStream source)
		{
			source.MoveNext(); // Skip Sentinal value

			while (Encoding.UTF8.GetPreamble().Contains(source.Current)) // Skip the BOM marks if there are any
			{
				source.MoveNext();
			}

			source.SkipWhiteSpace(); // Make sure we start at the beginning of a sample.
		}

		public IEnumerable<LinuxEvent> Parse(string filename)
		{
			return this.Parse(new FastStream(filename));
		}

		public IEnumerable<LinuxEvent> Parse(Stream stream)
		{
			return this.Parse(new FastStream(stream));
		}

		/// <summary>
		/// Parses the PerfScript .dump file given, gives one sample at a time
		/// </summary>
		public IEnumerable<LinuxEvent> Parse(FastStream source)
		{
			this.SkipPreamble(source);

			return this.ParseSamples(source);
		}

		public IEnumerable<LinuxEvent> ParseSamples(FastStream source)
		{
			if (source.Current == 0 && !source.EndOfStream)
			{
				source.MoveNext();
			}

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

		public void SetSymbolFile(ZipArchive archive)
		{
			this.mapper = new LinuxPerfScriptMapper(archive, this);
		}

		public void SetSymbolFile(string path)
		{
			this.SetSymbolFile(new ZipArchive(new FileStream(path, FileMode.Open)));
		}

		internal void ParseSymbolFile(Stream stream, Mapper mapper)
		{
			FastStream source = new FastStream(stream);
			source.MoveNext(); // Avoid \0 encounter
			this.SkipPreamble(source); // Remove encoding stuff if it's there
			source.SkipWhiteSpace();

			StringBuilder sb = new StringBuilder();

			Func<byte, bool> untilWhiteSpace = (byte c) => { return !char.IsWhiteSpace((char)c); };

			while (!source.EndOfStream)
			{
				source.ReadAsciiStringUpToTrue(sb, untilWhiteSpace);
				ulong start = ulong.Parse(sb.ToString(), System.Globalization.NumberStyles.HexNumber);
				sb.Clear();
				source.SkipWhiteSpace();

				source.ReadAsciiStringUpToTrue(sb, untilWhiteSpace);
				ulong size = ulong.Parse(sb.ToString(), System.Globalization.NumberStyles.HexNumber);
				sb.Clear();
				source.SkipWhiteSpace();

				source.ReadAsciiStringUpTo('\n', sb);
				string symbol = sb.ToString().TrimEnd();
				sb.Clear();

				mapper.Add(start, size, symbol);

				source.SkipWhiteSpace();
			}
		}

		public string[] GetSymbolFromMicrosoftMap(string entireSymbol, string mapFileLocation = "")
		{
			for (int first = 0; first < entireSymbol.Length;)
			{
				int last = entireSymbol.IndexOf(' ', first);
				if (last == -1)
				{
					last = entireSymbol.Length;
				}

				if (entireSymbol[first] == '[' && entireSymbol[last - 1] == ']')
				{
					var symbol = entireSymbol.Substring(System.Math.Min(entireSymbol.Length, last + 1));
					return new string[2] { entireSymbol.Substring(first + 1, last - first - 2), symbol.Trim() };
				}

				first = last + 1;
			}

			return new string[2] { entireSymbol, mapFileLocation };
		}

		public bool IsEndOfSample(FastStream source)
		{
			return this.IsEndOfSample(source, source.Current, source.Peek(1));
		}

		public bool IsEndOfSample(FastStream source, byte current, byte peek1)
		{
			return (current == '\n' && (peek1 == '\n' || peek1 == '\r' || peek1 == 0)) || source.EndOfStream;
		}

		#region private
		private LinuxPerfScriptMapper mapper;

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

			while (true)
			{
				source.SkipWhiteSpace();

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

					IEnumerable<Frame> frames = this.ReadFramesForSample(comm, pid, tid, threadTimeFrame, source);

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

		private List<Frame> ReadFramesForSample(string command, int processID, int threadID, Frame threadTimeFrame, FastStream source)
		{
			List<Frame> frames = new List<Frame>();

			if (threadTimeFrame != null)
			{
				frames.Add(threadTimeFrame);
			}

			while (!this.IsEndOfSample(source, source.Current, source.Peek(1)))
			{
				StackFrame stackFrame = this.ReadFrame(source);
				if (this.mapper != null && stackFrame.Address.Length > 1 && stackFrame.Address[0] == '0' && stackFrame.Address[1] == 'x')
				{
					string[] moduleSymbol = this.mapper.ResolveSymbols(processID, stackFrame);
					stackFrame = new StackFrame(stackFrame.Address, moduleSymbol[0], moduleSymbol[1]);
				}
				frames.Add(stackFrame);
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

			assumedModule = this.RemoveOuterBrackets(assumedModule.Trim());

			string actualModule = assumedModule;
			string actualSymbol = this.RemoveOuterBrackets(assumedSymbol.Trim());

			if (assumedModule.EndsWith(".map"))
			{
				string[] moduleSymbol = this.GetSymbolFromMicrosoftMap(assumedSymbol, assumedModule);
				actualSymbol = string.IsNullOrEmpty(moduleSymbol[1]) ? assumedModule : moduleSymbol[1];
				actualModule = moduleSymbol[0];
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

		private string RemoveOuterBrackets(string s)
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
		#endregion
	}

	#region Mapper
	internal class LinuxPerfScriptMapper
	{
		public static readonly Regex MapFilePatterns = new Regex(@"^perf\-[0-9]+\.map|.+\.ni\.\{.+\}\.map$");
		public static readonly Regex DllMapFilePattern = new Regex(@"^.+\.ni\.\{.+\}$");

		public LinuxPerfScriptMapper(ZipArchive archive, LinuxPerfScriptEventParser parser)
		{
			this.fileSymbolMappers = new Dictionary<string, Mapper>();
			this.parser = parser;

			if (archive != null)
			{
				this.PopulateSymbolMapper(archive);
			}
		}

		public string[] ResolveSymbols(int processID, StackFrame stackFrame)
		{
			ulong absoluteLocation = ulong.Parse(
				stackFrame.Address.Substring(2),
				System.Globalization.NumberStyles.HexNumber);

			Mapper mapper;
			if (this.fileSymbolMappers.TryGetValue(
				string.Format("perf-{0}", processID.ToString()), out mapper))
			{
				string symbol;
				ulong location;
				if (mapper.TryFindSymbol(absoluteLocation, out symbol, out location))
				{
					if (DllMapFilePattern.IsMatch(symbol))
					{
						ulong relativeLocation = absoluteLocation - location;
						if (this.fileSymbolMappers.TryGetValue(symbol, out mapper))
						{
							if (mapper.TryFindSymbol(relativeLocation, out symbol, out location))
							{
								return this.parser.GetSymbolFromMicrosoftMap(symbol);
							}
						}
					}
				}
			}

			return new string[] { stackFrame.Module, stackFrame.Symbol };
		}

		#region private
		private readonly Dictionary<string, Mapper> fileSymbolMappers;
		private readonly LinuxPerfScriptEventParser parser;

		private void PopulateSymbolMapper(ZipArchive archive)
		{
			Contract.Requires(archive != null, nameof(archive));

			foreach (var entry in archive.Entries)
			{
				if (MapFilePatterns.IsMatch(entry.FullName))
				{
					Mapper mapper = new Mapper();
					this.fileSymbolMappers[Path.GetFileNameWithoutExtension(entry.FullName)] = mapper;
					using (Stream stream = entry.Open())
					{
						this.parser.ParseSymbolFile(stream, mapper);
					}
					mapper.DoneMapping();
				}
			}
		}
		#endregion
	}

	internal class Mapper
	{
		private List<Map> maps;

		public Mapper()
		{
			this.maps = new List<Map>();
		}

		public void DoneMapping()
		{
			// Sort by the start part of the interval... This is for O(log(n)) search time.
			this.maps.Sort((Map x, Map y) => x.Interval.Start.CompareTo(y.Interval.Start));
		}

		public void Add(ulong start, ulong size, string symbol)
		{
			this.maps.Add(new Map(new Interval(start, size), symbol));
		}

		public bool TryFindSymbol(ulong location, out string symbol, out ulong startLocation)
		{
			symbol = "";
			startLocation = 0;

			int start = 0;
			int end = this.maps.Count;
			int mid = (end - start) / 2;

			while (true)
			{
				int index = start + mid;
				if (this.maps[index].Interval.IsWithin(location))
				{
					symbol = this.maps[index].MapTo;
					startLocation = this.maps[index].Interval.Start;
					return true;
				}
				else if (location < this.maps[index].Interval.Start)
				{
					end = index;
				}
				else if (location >= this.maps[index].Interval.End)
				{
					start = index;
				}

				if (mid < 1)
				{
					break;
				}

				mid = (end - start) / 2;
			}

			return false;
		}
	}

	internal struct Map
	{
		public Interval Interval { get; }
		public string MapTo { get; }

		public Map(Interval interval, string mapTo)
		{
			this.Interval = interval;
			this.MapTo = mapTo;
		}
	}

	internal class Interval
	{
		public ulong Start { get; }
		public ulong Length { get; }
		public ulong End { get { return this.Start + this.Length; } }

		// Taking advantage of unsigned arithmetic wrap-around to get it done in just one comparison.
		public bool IsWithin(ulong thing)
		{
			return (thing - this.Start) < this.Length;
		}

		public bool IsWithin(ulong thing, bool inclusiveStart, bool inclusiveEnd)
		{
			bool startEqual = inclusiveStart && thing.CompareTo(this.Start) == 0;
			bool endEqual = inclusiveEnd && thing.CompareTo(this.End) == 0;
			bool within = thing.CompareTo(this.Start) > 0 && thing.CompareTo(this.End) < 0;

			return within || startEqual || endEqual;
		}

		public Interval(ulong start, ulong length)
		{
			this.Start = start;
			this.Length = length;
		}

	}
	#endregion

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
		public int PreviousPriority { get; }
		public char PreviousState { get; }
		public string NextCommand { get; }
		public int NextThreadID { get; }
		public int NextPriority { get; }
		public int PreviousThreadID { get; }

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

	public enum FrameKind
	{
		StackFrame,
		ProcessFrame,
		ThreadFrame,
		BlockedCPUFrame
	}

	/// <summary>
	/// A way to define different types of frames with different names on PerfView.
	/// </summary>
	public interface Frame
	{
		FrameKind Kind { get; }
		string DisplayName { get; }
	}

	/// <summary>
	/// Defines a single stack frame on a linux sample.
	/// </summary>
	public struct StackFrame : Frame
	{
		public FrameKind Kind { get { return FrameKind.StackFrame; } }
		public string DisplayName { get { return string.Format("{0}!{1}", this.Module, this.Symbol); } }
		public string Address { get; }
		public string Module { get; }
		public string Symbol { get; }

		public StackFrame(string address, string module, string symbol)
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
		public FrameKind Kind { get { return FrameKind.ProcessFrame; } }
		public string DisplayName { get { return this.Name; } }
		public string Name { get; }

		public ProcessFrame(string name)
		{
			this.Name = name;
		}
	}

	/// <summary>
	/// Represents the name of the thread and its ID.
	/// </summary>
	public struct ThreadFrame : Frame
	{
		public FrameKind Kind { get { return FrameKind.ThreadFrame; } }
		public string DisplayName { get { return string.Format("{0} ({1})", this.Name, this.ID); } }
		public string Name { get; }
		public int ID { get; }

		public ThreadFrame(int id, string name)
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
		public FrameKind Kind { get { return FrameKind.BlockedCPUFrame; } }
		public string DisplayName { get { return this.SubKind; } }
		public string SubKind { get; }
		public int ID { get; }

		public BlockedCPUFrame(int id, string kind)
		{
			this.ID = id;
			this.SubKind = kind;
		}
	}
}
