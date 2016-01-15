using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClrProfiler;
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
		internal Dictionary<int, string> IDToFrame; // Given an ID, the frame, used for exporting to XML
		internal Dictionary<int, KeyValuePair<int, int>> Stacks; // Stack ID -> Frame ID / Caller ID
		internal Dictionary<int, KeyValuePair<int, double>> Samples; // Sample ID -> Stack ID / Time

		internal PerfScriptTraceEventParser(string sourcePath)
		{
			Requires.NotNull(sourcePath, nameof(sourcePath));

			this.FrameToID = new Dictionary<string, int>();
			this.IDToFrame = new Dictionary<int, string>();
			this.Stacks = new Dictionary<int, KeyValuePair<int, int>>();
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

		private IEnumerable<LinuxEvent> NextEvent(Regex regex)
		{

			string line = string.Empty;

			while (true)
			{
				if (this.source.EndOfStream)
				{
					break;
				}

				StringBuilder sb = new StringBuilder();

				this.source.SkipWhiteSpace();

				// comm
				this.source.ReadAsciiStringUpTo(' ', sb);
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
				double time;
				double.TryParse(sb.ToString(), out time);
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

				// event props
				this.source.ReadAsciiStringUpTo('\n', sb);
				string eventProp = sb.ToString();
				sb.Clear();

				if (regex != null && !regex.IsMatch(eventName))
				{
					while (true)
					{
						this.source.MoveNext();
						if ((this.source.Current == '\n' && this.source.Peek(1) == '\n') || this.source.EndOfStream)
						{
							break;
						}
					}

					yield return null;
				}
				else
				{
					int id = this.GetSampleForEvent(time);
					yield return new LinuxEvent(comm, tid, pid, time, timeProp, cpu, eventName, eventProp, id);
				}
			}
		}

		private int GetSampleForEvent(double time)
		{
			int startStack = this.StackID;
			this.DoStackTrace(0, startStack);

			int sampleID = this.SampleID++;
			this.Samples.Add(sampleID, new KeyValuePair<int, double>(this.StackID - 1, time));
			return sampleID;
		}

		private int DoStackTrace(int offset, int currentStack)
		{
			string line;
			if ((line = this.source.ReadLine()).Length == 0) return offset - 1;

			int frameID;
			string address = line.Trim();
			if (!this.FrameToID.TryGetValue(address, out frameID))
			{
				frameID = this.FrameID++;
				this.FrameToID.Add(address, frameID);
				this.IDToFrame.Add(frameID, address);
			}

			int startID = this.DoStackTrace(offset + 1, currentStack);

			int stackID = this.StackID++;
			int deltaStack = startID - (offset + 1);

			this.Stacks.Add(stackID, new KeyValuePair<int, int>(frameID, deltaStack == -1 ? deltaStack : deltaStack + currentStack));

			return startID;
		}

		private FastStream source;
		private List<LinuxEvent> events;
	}

	internal static class FastStreamExtension
	{
		internal static string ReadLine(this FastStream stream)
		{
			StringBuilder sb = new StringBuilder();
			//sb.Append((char)stream.Current);
			char next;
			while (((next = (char)stream.ReadChar()) != '\n' && next != '\0') && !stream.EndOfStream)
			{
				sb.Append(next);
			}

			return sb.ToString();
		}
	}
}
