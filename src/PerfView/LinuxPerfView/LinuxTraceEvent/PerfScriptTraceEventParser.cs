using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
			this.source = File.OpenText(sourcePath);
			this.Parse();
		}

		internal void Parse()
		{
			foreach (LinuxEvent linuxEvent in this.NextEvent())
			{
				this.events.Add(linuxEvent);
			}
		}

		private IEnumerable<LinuxEvent> NextEvent()
		{

			string line = string.Empty;

			while (this.source.Peek() != -1)
			{

				while ((line = this.source.ReadLine()).Length == 0) ;

				string[] fullHeader = line.Split(' ', '/');

				List<string> header = new List<string>();

				foreach (string s in fullHeader)
				{
					if (s.Length != 0)
					{
						header.Add(s);
					}
				}

				string comm = header[0];

				int pid;
				int.TryParse(header[1], out pid);

				int tid;
				int.TryParse(header[2], out tid);

				int cpu;
				int.TryParse(header[3].Substring(1, header[3].Length - 2), out cpu);

				double time;
				double.TryParse(header[4].Substring(0, header[4].Length - 2), out time);

				string eventName = header[5];

				int id = this.GetSampleForEvent(time);

				yield return new LinuxEvent(comm, tid, pid, time, cpu, eventName, id);//, stackTrace);
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

			this.Stacks.Add(stackID, new KeyValuePair<int, int>(frameID, deltaStack  == -1 ? deltaStack : deltaStack + currentStack));

			return startID;
		}



		private TextReader source;
		private List<LinuxEvent> events;
	}
}
