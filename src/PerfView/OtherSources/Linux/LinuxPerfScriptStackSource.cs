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
				double portion = 1;

				isComplete = this.TryGetCompleteBuffer(source, startLook, portion, source.MaxPeek - TruncateString.Length, out length);

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

			while (true)
			{
				if (length >= maxLength)
				{
					length = lastNewLine;

					if (portion < 0.5)
					{
						return false;
					}

					return this.TryGetCompleteBuffer(source, startLook, portion * 0.8, maxLength, out length);
				}

				byte current = source.Peek(length);

				if (this.parser.IsEndOfSample(source, current, source.Peek(length + 1)))
				{
					break;
				}

				if (current == '\n')
				{
					lastNewLine = length;
				}

				length++;
			}

			return true;
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

			if (this.doThreadTime)
			{
				this.blockedThreads = new Dictionary<int, double>();
				this.threadBlockedPeriods = new List<ThreadPeriod>();
				this.cpuThreadUsage = new Dictionary<int, int>();
			}

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
			Contract.Requires(this.threadBlockedPeriods != null, nameof(threadBlockedPeriods));
			double timeBlocked = 0;
			foreach (ThreadPeriod period in this.threadBlockedPeriods)
			{
				timeBlocked += period.Period;
			}

			return timeBlocked;
		}

		public StackSourceSample GetSampleFor(LinuxEvent linuxEvent)
		{
			if (this.doThreadTime)
			{
				// If doThreadTime is true this is running on a single thread.
				this.AnalyzeSampleForBlockedTime(linuxEvent);
			}

			IEnumerable<Frame> frames = linuxEvent.CallerStacks;
			StackSourceCallStackIndex stackIndex = this.currentStackIndex;

			stackIndex = this.InternFrames(frames.GetEnumerator(), stackIndex, linuxEvent.ProcessID, linuxEvent.ThreadID, this.doThreadTime);

			var sample = new StackSourceSample(this);
			sample.StackIndex = stackIndex;
			sample.TimeRelativeMSec = linuxEvent.Time;
			sample.Metric = 1;

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
			foreach (var linuxEvent in this.parser.Parse(this.masterSource))
			{
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

		#region private
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

		private void InternAllLinuxEvents(Stream stream)
		{
			this.DoInterning();

			if (this.doThreadTime)
			{
				this.FlushBlockedThreadsAt((double)this.SampleEndTime);
				this.threadBlockedPeriods.Sort((x, y) => x.StartTime.CompareTo(y.StartTime));
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

				if (this.blockedThreads.ContainsKey((int)threadid))
				{
					frameDisplayName = StateThread.BLOCKED_TIME.ToString();
				}
				else
				{
					frameDisplayName = StateThread.CPU_TIME.ToString();
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

		private void FlushBlockedThreadsAt(double endTime)
		{
			foreach (int threadid in this.blockedThreads.Keys)
			{
				double startTime = this.blockedThreads[threadid];
				this.threadBlockedPeriods.Add(new ThreadPeriod(startTime, endTime));
			}

			this.blockedThreads.Clear();
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

		private readonly Dictionary<int, double> blockedThreads;
		private readonly List<ThreadPeriod> threadBlockedPeriods;
		private readonly Dictionary<int, int> cpuThreadUsage;

		private double? SampleEndTime;

		private StackSourceCallStackIndex currentStackIndex;
		#endregion
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
