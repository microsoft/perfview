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

		internal int FrameID = 0;
		internal int StackID = 0;
		internal int SampleID = 0;

		// Optimized later to only have arrays of each of these types...
		internal Dictionary<string, int> FrameToID; // Given a frame, the ID, used for frame look up
		private Dictionary<int, FrameInfo> IDToFrame; // Given an ID, the frame, used for exporting to XML
		private Dictionary<int, FrameStack> Stacks; // Stack ID -> Frame ID / Caller ID
		private Dictionary<long, FrameStack> FrameStacks;
		internal Dictionary<int, KeyValuePair<int, double>> Samples; // Sample ID -> Stack ID / Time

		internal PerfScriptTraceEventParser(string sourcePath)
		{
			Requires.NotNull(sourcePath, nameof(sourcePath));

			this.FrameToID = new Dictionary<string, int>();
			this.IDToFrame = new Dictionary<int, FrameInfo>();
			this.Stacks = new Dictionary<int, FrameStack>();
			this.Stacks.Add(-1, new FrameStack(-1, -1, null));
			this.FrameStacks = new Dictionary<long, FrameStack>();
			this.Samples = new Dictionary<int, KeyValuePair<int, double>>();

			this.events = new List<LinuxEvent>();
			this.source = new FastStream(sourcePath);
		}

		internal void Parse(string regexFilter)
		{
			Regex rgx = regexFilter == null ? null : new Regex(regexFilter);
			foreach (LinuxEvent linuxEvent in this.NextEvent(rgx))
			{
				if (linuxEvent != null)
				{
					this.events.Add(linuxEvent);
				}
			}
		}

		internal string GetFrameAt(int i)
		{
			return this.IDToFrame[i].Module + "!" + this.IDToFrame[i].Symbol;
		}

		internal int GetCallerAtStack(int i)
		{
			return this.Stacks[i].Caller.ID;
		}

		internal int GetFrameAtStack(int i)
		{
			return this.Stacks[i].FrameID;
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

				StringBuilder sb = new StringBuilder();

				// comm
				this.source.ReadAsciiStringUpToWhiteSpace(sb);
				string comm = sb.ToString();
				sb.Clear();

				// pid
				this.source.SkipWhiteSpace();
				int pid = this.source.ReadInt();
				this.source.MoveNext();

				//tid
				int tid = this.source.ReadInt();

				// cpu
				this.source.SkipWhiteSpace();
				this.source.MoveNext();
				int cpu = this.source.ReadInt();
				this.source.MoveNext();

				// time
				this.source.SkipWhiteSpace();
				this.source.ReadAsciiStringUpTo(':', sb);
				double time = double.Parse(sb.ToString());
				sb.Clear();

				// time-attri
				this.source.MoveNext();
				this.source.SkipWhiteSpace();
				int timeProp = this.source.ReadInt(); // for now we just move past it...

				// event name
				this.source.SkipWhiteSpace();
				this.source.ReadAsciiStringUpTo(':', sb);
				string eventName = sb.ToString();
				sb.Clear();
				this.source.MoveNext();

				// event props
				this.source.ReadAsciiStringUpTo('\n', sb);
				string eventProp = sb.ToString();
				sb.Clear();
				this.source.MoveNext();

				// int id = this.ReadStackTraceForEvent(time);
				// yield return new LinuxEvent(comm, tid, pid, time, timeProp, cpu, eventName, eventProp, id);

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
					int id = this.ReadStackTraceForEvent(time);
					yield return new LinuxEvent(comm, tid, pid, time, timeProp, cpu, eventName, eventProp, id);
				}
			}
		}

		private int ReadStackTraceForEvent(double time)
		{
			int startStack = this.StackID;
			this.DoStackTrace();

			int sampleID = this.SampleID++;
			this.Samples.Add(sampleID, new KeyValuePair<int, double>(this.StackID - 1, time));
			return sampleID;
		}

		private FrameStack DoStackTrace()
		{
			if ((this.source.Current == '\n' &&
				(this.source.Peek(1) == '\n' || this.source.Peek(1) == '\r' || this.source.Peek(1) == '\0') ||
				 this.source.EndOfStream))
			{
				this.source.MoveNext();
				return Stacks[-1]; // Returns the null caller
			}

			StringBuilder sb = new StringBuilder();

			this.source.SkipWhiteSpace();
			this.source.ReadAsciiStringUpTo(' ', sb);
			string address = sb.ToString();
			sb.Clear();

			int frameID;

			if (!this.FrameToID.TryGetValue(address, out frameID))
			{
				frameID = this.FrameID++;
				this.FrameToID.Add(address, frameID);
				this.IDToFrame.Add(frameID, this.ReadFrameInfo(address));
			}
			else
			{
				// We don't care about this frame since we already have it stashed
				this.source.SkipUpTo('\n');
			}

			FrameStack caller = this.DoStackTrace();

			long framestackid = Utils.ConcatIntegers(caller.ID, frameID);
			FrameStack framestack;
			if (!FrameStacks.TryGetValue(framestackid, out framestack))
			{
				framestack = new FrameStack(this.StackID++, frameID, Stacks[caller.ID]);
				this.FrameStacks.Add(framestackid, framestack);
				this.Stacks.Add(framestack.ID, framestack);
			}

			return framestack;
		}

		private FastStream source;
		private List<LinuxEvent> events;

		private FrameInfo ReadFrameInfo(string address)
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

			return new FrameInfo(address, actualModule, actualSymbol);
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

		private class FrameInfo
		{
			internal string Address { get; }
			internal string Module { get; }
			internal string Symbol { get; }

			internal FrameInfo(string address, string module, string symbol)
			{
				this.Address = address;
				this.Module = module;
				this.Symbol = symbol;
			}
		}

		private class FrameStack
		{
			internal int ID { get; }
			internal int FrameID { get; }
			internal FrameStack Caller { get; }

			internal FrameStack(int id, int frameid, FrameStack caller)
			{
				this.ID = id;
				this.FrameID = frameid;
				this.Caller = caller;
			}
		}
	}

	internal static class FastStreamExtension
	{
		internal static string ReadLine(this FastStream stream)
		{
			StringBuilder sb = new StringBuilder();
			char next;
			while (((next = (char)stream.ReadChar()) != '\n' && next != '\0') && !stream.EndOfStream)
			{
				sb.Append(next);
			}

			return sb.ToString();
		}

		internal static void ReadBytesUpTo(this FastStream stream, char c, byte[] bytes, out int length)
		{
			int numbytes = 0;
			while (stream.Current != c && numbytes < bytes.Length)
			{
				bytes[numbytes++] = stream.Current;
				stream.MoveNext();
			}

			length = numbytes;
		}

		internal static void ReadAsciiStringUpToLastOnLine(this FastStream stream, char c, StringBuilder sb)
		{
			StringBuilder buffer = new StringBuilder();
			FastStream.MarkedPosition mp = stream.MarkPosition();

			while (stream.Current != '\n' && !stream.EndOfStream)
			{
				if (stream.Current == c)
				{
					sb.Append(buffer);
					buffer.Clear();
					mp = stream.MarkPosition();
				}

				buffer.Append((char)stream.Current);
				stream.MoveNext();
			}

			stream.RestoreToMark(mp);
		}

		internal static void ReadAsciiStringUpToWhiteSpace(this FastStream stream, StringBuilder sb)
		{
			while (!char.IsWhiteSpace((char)stream.Current))
			{
				sb.Append((char)stream.Current);
				if (!stream.MoveNext())
				{
					break;
				}
			}
		}
	}
}
