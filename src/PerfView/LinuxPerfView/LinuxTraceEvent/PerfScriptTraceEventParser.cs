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

		private Dictionary<int, ProcessNode> Processes;
		private Dictionary<long, ThreadNode> Threads;

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

			this.Processes = new Dictionary<int, ProcessNode>();
			this.Threads = new Dictionary<long, ThreadNode>();
		}

		internal void Parse(string regexFilter, int maxSamples)
		{
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
			return this.Stacks[i].Caller.ID;
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

				// event details
				this.source.ReadAsciiStringUpTo('\n', sb);
				string eventDetails = sb.ToString();
				sb.Clear();
				this.source.MoveNext();

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
					LinuxEvent linuxEvent =
						new LinuxEvent(comm, tid, pid, time, timeProp, cpu, eventName, eventDetails, this.SampleCount++);
					this.ReadStackTraceForEvent(linuxEvent);
					yield return linuxEvent;
				}
			}
		}

		private void ReadStackTraceForEvent(LinuxEvent linuxEvent)
		{
			int startStack = this.StackCount;
			this.DoStackTrace(linuxEvent);
			this.Samples.Add(new SampleInfo(this.StackCount - 1, linuxEvent.Time));
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

				// We are at the end of the physical stack trace on sample on the trace, but we need to add two
				//   extra stacks for convenience and display purposes
				ProcessNode processNode;
				if (!this.Processes.TryGetValue(linuxEvent.ProcessID, out processNode))
				{
					frameCount = this.FrameCount++;

					FrameInfo frameInfo = new ProcessThreadFrame(linuxEvent.ProcessID, linuxEvent.EventName);

					this.IDFrame.Add(frameInfo);
					this.FrameID.Add(frameInfo.DisplayName, frameCount);

					processNode = new ProcessNode(linuxEvent.ProcessID, linuxEvent.EventName, frameCount, this.Stacks[-1]);
					this.Processes.Add(linuxEvent.ProcessID, processNode);
					
					this.Stacks.Add(this.StackCount++, processNode);
				}

				// This might not be needed, but this is to make sure that when we look up the thread, we know
				//   it belongs to a specific process
				long processThreadID = Utils.ConcatIntegers(processNode.ID, linuxEvent.ThreadID);

				ThreadNode threadNode;
				if (!this.Threads.TryGetValue(processThreadID, out threadNode))
				{
					frameCount = this.FrameCount++;

					FrameInfo frameInfo = new ProcessThreadFrame(linuxEvent.ThreadID, "Thread");

					this.IDFrame.Add(frameInfo);
					this.FrameID.Add(frameInfo.DisplayName, frameCount);

					threadNode = new ThreadNode(linuxEvent.ThreadID, frameCount, processNode);
					this.Threads.Add(processThreadID, threadNode);

					this.Stacks.Add(this.StackCount++, threadNode);
				}

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
			long framestackid = Utils.ConcatIntegers(caller.ID, frameID);

			FrameStack framestack;
			if (!FrameStacks.TryGetValue(framestackid, out framestack))
			{
				framestack = new FrameStack(this.StackCount++, frameID, caller);
				this.FrameStacks.Add(framestackid, framestack);
				this.Stacks.Add(framestack.ID, framestack);
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

		private interface FrameInfo
		{
			string DisplayName { get; }
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

		private class FrameStack : StackNode
		{
			internal FrameStack(int id, int frameID, StackNode caller) :
				base(StackKind.FrameStack, id, frameID, caller)
			{
			}
		}

		private class ProcessNode : StackNode
		{
			internal string Name { get; }

			internal ProcessNode(int id, string name, int frameID, StackNode invalidNode) :
				base(StackKind.Process, id, frameID, invalidNode)
			{
				this.Name = name;
			}
		}

		private class ThreadNode : StackNode
		{
			/// <summary>
			/// Returns the same thing as Next but is automatically a ProcessStack
			/// instead of a StackNode
			/// </summary>
			internal ProcessNode Process { get; }

			internal ThreadNode(int id, int frameID, ProcessNode process) : base(StackKind.Thread, id, frameID, process)
			{
				this.Process = process;
			}
		}

		private abstract class StackNode
		{
			internal int ID { get; }
			internal StackKind Kind { get; }
			/// <summary>
			/// Returns the next node on the stack
			/// </summary>
			internal StackNode Caller { get; }

			internal int FrameID { get; }

			internal StackNode(StackKind kind, int id, int frameID, StackNode caller)
			{
				this.ID = id;
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
