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

		public PerfScriptEventParser(string sourcePath, bool analyzeBlockedTime)
		{
			Requires.NotNull(sourcePath, nameof(sourcePath));

			this.SourcePath = sourcePath;
		}

		#region Fields
		private FastStream source;
		private List<LinuxEvent> events;


		private double CurrentTime { get; set; }
		private bool startTimeSet = false;
		private double StartTime { get; set; }
		private string SourcePath { get; }
		private ZipArchive Archive { get; set; }
		#endregion

		private void Initialize()
		{
			this.source = this.GetFastStream(this.SourcePath); // new FastStream(this.SourcePath);
			this.events = new List<LinuxEvent>();
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

		public void Dispose()
		{
			this.Archive?.Dispose();
		}

	}
}
