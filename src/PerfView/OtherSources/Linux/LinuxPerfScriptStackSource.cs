using System;
using System.Collections.Concurrent;
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
	public class ParallelLinuxPerfScriptStackSource : LinuxPerfScriptStackSource
	{
		public ParallelLinuxPerfScriptStackSource(string path, bool doThreadTime = false) : base(path, doThreadTime)
		{
		}

		protected override void DoInterning()
		{
			int threadCount = this.doThreadTime ? 1 : MaxThreadCount;

			this.frames = new ConcurrentDictionary<string, StackSourceFrameIndex>();

			this.parser.SkipPreamble(masterSource);

			Task[] tasks = new Task[threadCount];
			List<StackSourceSample>[] threadSamples = new List<StackSourceSample>[tasks.Length];

			for (int i = 0; i < tasks.Length; i++)
			{
				threadSamples[i] = new List<StackSourceSample>();
				tasks[i] = new Task((object givenArrayIndex) =>
				{
					FastStream bufferPart;
					while ((bufferPart = this.GetNextSubStream(masterSource)) != null)
					{
						foreach (LinuxEvent linuxEvent in this.parser.ParseSamples(bufferPart))
						{
							if (this.doThreadTime)
							{
								// If doThreadTime is true this is running on a single thread.
								this.blockedTimeAnalyzer.UpdateThreadState(linuxEvent);
							}
							StackSourceSample sample = this.GetSampleFor(linuxEvent);
							threadSamples[(int)givenArrayIndex].Add(sample);
						}

						bufferPart.Dispose();
					}
				}, i);

				tasks[i].Start();
			}

			Task.WaitAll(tasks);

			IEnumerable<StackSourceSample> allSamplesEnumerator = null;
			foreach (var samples in threadSamples)
			{
				if (allSamplesEnumerator == null)
				{
					allSamplesEnumerator = samples;
				}
				else
				{
					allSamplesEnumerator = allSamplesEnumerator.Concat(samples);
				}
			}

			this.AddSamples(allSamplesEnumerator);
		}

		protected override StackSourceFrameIndex InternFrame(string displayName)
		{
			StackSourceFrameIndex frameIndex;
			if (!frames.TryGetValue(displayName, out frameIndex))
			{
				lock (internFrameLock)
				{
					frameIndex = this.Interner.FrameIntern(displayName);
					frames[displayName] = frameIndex;
				}
			}

			return frameIndex;
		}

		protected override StackSourceCallStackIndex InternCallerStack(StackSourceFrameIndex frameIndex, StackSourceCallStackIndex stackIndex)
		{
			lock (internCallStackLock)
			{
				return this.Interner.CallStackIntern(frameIndex, stackIndex);
			}
		}

		#region private
		// If the length returned is -1, then there's no more stream in the
		//   master source, otherwise, buffer should be valid with the length returned
		// Note: This needs to be thread safe
		private FastStream GetNextSubStream(FastStream source)
		{
			lock (bufferLock)
			{
				if (source.EndOfStream)
				{
					return null;
				}

				uint startLook = (uint)this.BufferSize * 3 / 4;
				uint length;

				bool isComplete;

				isComplete = this.TryGetCompleteBuffer(source, startLook, 1, source.MaxPeek - TruncateString.Length, out length);

				FastStream subStream = source.ReadSubStream((int)length, trail: (!isComplete ? TruncateString : null));

				if (!isComplete)
				{
					this.FindValidStartOn(source);
				}

				source.SkipWhiteSpace();

				return subStream;
			}
		}

		private const string TruncateString = "0 truncate (truncate)";

		private bool TryGetCompleteBuffer(FastStream source, uint startLook, double portion, int maxLength, out uint length)
		{
			Contract.Requires(source != null, nameof(source));

			length = (uint)(startLook * portion);

			if (source.Peek(startLook) == 0)
			{
				return true;
			}

			uint lastNewLine = length;

			for (uint i = length; i < maxLength; i++)
			{
				byte current = source.Peek(i);

				if (this.parser.IsEndOfSample(source, current, source.Peek(i + 1)))
				{
					length = i;
					return true;
				}

				if (current == '\n')
				{
					lastNewLine = length;
				}
			}

			if (portion < 0.5)
			{
				length = lastNewLine;
				return false;
			}

			return this.TryGetCompleteBuffer(source, startLook, portion * 0.8, maxLength, out length);
		}

		// Assumes that source is at an invalid start position.
		private void FindValidStartOn(FastStream source)
		{
			while (!this.parser.IsEndOfSample(source))
			{
				source.MoveNext();
			}
		}

		private ConcurrentDictionary<string, StackSourceFrameIndex> frames;
		private object internFrameLock = new object();
		private object internCallStackLock = new object();

		private object bufferLock = new object();
		private const int bufferDivider = 4;

		private const int MaxThreadCount = 4;
		#endregion
	}

	public class LinuxPerfScriptStackSource : InternStackSource
	{
		public LinuxPerfScriptStackSource(string path, bool doThreadTime)
		{
			this.doThreadTime = doThreadTime;
			this.currentStackIndex = 0;

			ZipArchive archive;
			using (Stream stream = this.GetPerfScriptStream(path, out archive))
			{
				this.masterSource = new FastStream(stream, this.BufferSize);
				this.parser = new LinuxPerfScriptEventParser();
				this.parser.SetSymbolFile(archive);

				this.InternAllLinuxEvents(stream);
				stream.Close();
			}
			archive?.Dispose();
		}

		public static readonly string[] PerfDumpSuffixes = new string[]
		{
			".data.dump", ".data.txt", ".trace.zip"
		};

		public double GetTotalBlockedTime()
		{
			return this.blockedTimeAnalyzer.TotalBlockedTime;
		}

		public StackSourceSample GetSampleFor(LinuxEvent linuxEvent)
		{
			IEnumerable<Frame> frames = linuxEvent.CallerStacks;
			StackSourceCallStackIndex stackIndex = this.currentStackIndex;

			var sample = new StackSourceSample(this);
			sample.TimeRelativeMSec = linuxEvent.Time;
			sample.Metric = (float)linuxEvent.Period;

			stackIndex = this.InternFrames(frames.GetEnumerator(), stackIndex, linuxEvent.ProcessID, linuxEvent.ThreadID, this.doThreadTime);
			sample.StackIndex = stackIndex;

			return sample;
		}

		public void AddSamples(IEnumerable<StackSourceSample> _samples)
		{
			Contract.Requires(_samples != null, nameof(_samples));

			List<StackSourceSample> samples = _samples.ToList();
			samples.Sort((x, y) => x.TimeRelativeMSec.CompareTo(y.TimeRelativeMSec));
			double startTime = samples[0].TimeRelativeMSec;
			foreach (var sample in samples)
			{
				sample.TimeRelativeMSec -= startTime;
				this.AddSample(sample);
			}

			this.SampleEndTime = samples.Last().TimeRelativeMSec;
		}

		protected virtual void DoInterning()
		{
			if (this.doThreadTime)
			{
				this.blockedTimeAnalyzer = new BlockedTimeAnalyzer();
			}

			foreach (var linuxEvent in this.parser.Parse(this.masterSource))
			{
				if (this.doThreadTime)
				{
					this.blockedTimeAnalyzer.UpdateThreadState(linuxEvent);
				}

				this.AddSample(this.GetSampleFor(linuxEvent));
			}
		}

		protected virtual StackSourceCallStackIndex InternCallerStack(StackSourceFrameIndex frameIndex, StackSourceCallStackIndex stackIndex)
		{
			return this.Interner.CallStackIntern(frameIndex, stackIndex);
		}

		protected virtual StackSourceFrameIndex InternFrame(string displayName)
		{
			return this.Interner.FrameIntern(displayName);
		}

		protected readonly LinuxPerfScriptEventParser parser;
		protected readonly FastStream masterSource;
		protected readonly bool doThreadTime;
		protected readonly int BufferSize = 262144;

		protected BlockedTimeAnalyzer blockedTimeAnalyzer;

		#region private
		private void InternAllLinuxEvents(Stream stream)
		{
			this.DoInterning();

			if (this.doThreadTime)
			{
				this.blockedTimeAnalyzer.FinishAnaylizing();
				// TODO: Sort things in blocked time anaylizer
				// this.threadBlockedPeriods.Sort((x, y) => x.StartTime.CompareTo(y.StartTime));
			}

			this.Interner.DoneInterning();
		}

		private StackSourceCallStackIndex InternFrames(IEnumerator<Frame> frameIterator, StackSourceCallStackIndex stackIndex, int processID, int? threadid = null, bool doThreadTime = false)
		{
			// We shouldn't advance the iterator if thread time is enabled because we need 
			//   to add an extra frame to the caller stack that is not in the frameIterator.
			//   i.e. Short-circuiting prevents the frameIterator from doing MoveNext :)
			if (!doThreadTime && !frameIterator.MoveNext())
			{
				return StackSourceCallStackIndex.Invalid;
			}

			StackSourceFrameIndex frameIndex;
			string frameDisplayName;

			if (doThreadTime)
			{
				// If doThreadTime is true, then we need to make sure that threadid is not null
				Contract.Requires(threadid != null, nameof(threadid));

				if (this.blockedTimeAnalyzer.IsThreadBlocked((int)threadid))
				{
					frameDisplayName = LinuxThreadState.BLOCKED_TIME.ToString();
				}
				else
				{
					frameDisplayName = LinuxThreadState.CPU_TIME.ToString();
				}
			}
			else
			{
				frameDisplayName = frameIterator.Current.DisplayName;
			}

			frameIndex = this.InternFrame(frameDisplayName);

			stackIndex = this.InternCallerStack(frameIndex, this.InternFrames(frameIterator, stackIndex, processID));

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

				return foundEntry?.Open();
			}
			else
			{
				if (path.EndsWithOneOf(PerfDumpSuffixes))
				{
					return new FileStream(path, FileMode.Open);
				}
			}

			throw new Exception("Not a valid input file");
		}

		private double? SampleEndTime;

		private StackSourceCallStackIndex currentStackIndex;
		#endregion
	}

	public enum LinuxThreadState
	{
		BLOCKED_TIME,
		CPU_TIME
	}

	public class BlockedTimeAnalyzer
	{
		public double TimeStamp { get; private set; }
		public Dictionary<int, KeyValuePair<LinuxThreadState, LinuxEvent>> BeginningStates { get; }
		public Dictionary<int, KeyValuePair<LinuxThreadState, LinuxEvent>> EndingStates { get; }
		public Dictionary<LinuxEvent, StackSourceSample> LinuxEventSamples { get;}
		public Dictionary<int, int> EndingCpuUsage { get; }

		public double TotalBlockedTime { get; private set; }

		public BlockedTimeAnalyzer()
		{
			this.BeginningStates = new Dictionary<int, KeyValuePair<LinuxThreadState, LinuxEvent>>();
			this.EndingStates = new Dictionary<int, KeyValuePair<LinuxThreadState, LinuxEvent>>();
			this.LinuxEventSamples = new Dictionary<LinuxEvent, StackSourceSample>();
			this.EndingCpuUsage = new Dictionary<int, int>();
			this.TotalBlockedTime = 0;
		}

		public void UpdateThreadState(LinuxEvent linuxEvent)
		{
			if (this.TimeStamp < linuxEvent.Time)
			{
				this.TimeStamp = linuxEvent.Time;
			}

			if (!this.BeginningStates.ContainsKey(linuxEvent.ThreadID))
			{
				this.BeginningStates.Add(
					linuxEvent.ThreadID,
					new KeyValuePair<LinuxThreadState, LinuxEvent>(LinuxThreadState.CPU_TIME, linuxEvent));

				this.EndingStates[linuxEvent.ThreadID] = this.BeginningStates[linuxEvent.ThreadID];
			}

			this.DoMetrics(linuxEvent);
		}

		public bool IsThreadBlocked(int threadId)
		{
			return this.EndingStates.ContainsKey(threadId) && this.EndingStates[threadId].Key == LinuxThreadState.BLOCKED_TIME;
		}

		public void FinishAnaylizing()
		{
			this.FlushBlockedThreadsAt(this.TimeStamp);
		}

		private void FlushBlockedThreadsAt(double endTime)
		{
			foreach (int threadid in this.EndingStates.Keys)
			{
				if (this.EndingStates[threadid].Key == LinuxThreadState.BLOCKED_TIME)
				{
					this.TotalBlockedTime += this.TimeStamp - this.EndingStates[threadid].Value.Time;
				}
			}
		}

		private void DoMetrics(LinuxEvent linuxEvent)
		{
			KeyValuePair<LinuxThreadState, LinuxEvent> sampleInfo;

			if (this.EndingStates.TryGetValue(linuxEvent.ThreadID, out sampleInfo))
			{
				linuxEvent.Period = linuxEvent.Time - sampleInfo.Value.Time;
			}

			// This is check for completed scheduler events, ones that start with prev_comm and have 
			//   corresponding next_comm.
			if (linuxEvent.Kind == EventKind.Scheduler)
			{
				SchedulerEvent schedEvent = (SchedulerEvent)linuxEvent;
				if (this.EndingStates.ContainsKey(schedEvent.Switch.PreviousThreadID) &&
					this.EndingStates[schedEvent.Switch.PreviousThreadID].Key == LinuxThreadState.CPU_TIME) // Blocking
				{
					sampleInfo = this.EndingStates[schedEvent.Switch.PreviousThreadID];

					this.EndingStates[schedEvent.Switch.PreviousThreadID] =
						new KeyValuePair<LinuxThreadState, LinuxEvent>(LinuxThreadState.BLOCKED_TIME, linuxEvent);

					linuxEvent.Period = linuxEvent.Time - sampleInfo.Value.Time;
				}

				if (this.EndingStates.TryGetValue(schedEvent.Switch.NextThreadID, out sampleInfo) &&
					sampleInfo.Key == LinuxThreadState.BLOCKED_TIME) // Unblocking
				{
					this.EndingStates[schedEvent.Switch.NextThreadID] =
						new KeyValuePair<LinuxThreadState, LinuxEvent>(LinuxThreadState.CPU_TIME, linuxEvent);

					sampleInfo.Value.Period = linuxEvent.Time - sampleInfo.Value.Time;
					this.TotalBlockedTime += sampleInfo.Value.Period;
				}

			}
			else if (linuxEvent.Kind == EventKind.Cpu)
			{
				int threadid;
				if (this.EndingCpuUsage.TryGetValue(linuxEvent.Cpu, out threadid) && threadid != linuxEvent.ThreadID) // Unblocking
				{
					if (this.EndingStates.TryGetValue(threadid, out sampleInfo))
					{
						this.EndingStates[threadid] =
							new KeyValuePair<LinuxThreadState, LinuxEvent>(LinuxThreadState.CPU_TIME, linuxEvent);
						sampleInfo.Value.Period = linuxEvent.Time - sampleInfo.Value.Time;
						this.TotalBlockedTime += sampleInfo.Value.Period;
					}
				}
			}

			this.EndingCpuUsage[linuxEvent.Cpu] = linuxEvent.ThreadID;
		}
	}

	public class ThreadPeriod
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

	public static class StringExtension
	{
		public static bool EndsWithOneOf(this string path, string[] suffixes, StringComparison stringComparison = StringComparison.Ordinal)
		{
			foreach (string suffix in suffixes)
			{
				if (path.EndsWith(suffix, stringComparison))
				{
					return true;
				}
			}

			return false;
		}
	}
}
