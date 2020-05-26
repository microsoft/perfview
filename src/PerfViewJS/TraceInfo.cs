// <copyright file="TraceInfo.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System;

    public sealed class TraceInfo
    {
        public TraceInfo(string machineName, string osname, string osbuild, int? utoffset, TimeSpan currentOffset, DateTime bootTime, DateTime startTime,  DateTime endTime, double endTimeRelativeMSec, TimeSpan duration, int cpuspeed, int numberOfProcs, int memorySize, int pointerSize, TimeSpan profileInt, int eventCount, int lostEvents, long filesize)
        {
            this.MachineName = machineName;
            this.OperatingSystemName = osname;
            this.OperatingSystemBuildNumber = osbuild;
            this.UTCDiff = utoffset;
            this.UTCOffsetCurrentProcess = currentOffset.TotalMinutes;
            this.BootTime = bootTime;
            this.StartTime = startTime;
            this.EndTime = endTime;
            this.EndTimeRelativeMSec = endTimeRelativeMSec.ToString("F3");
            this.Duration = duration.TotalSeconds;
            this.ProcessorSpeed = cpuspeed;
            this.NumberOfProcessors = numberOfProcs;
            this.MemorySize = memorySize;
            this.PointerSize = pointerSize;
            this.SampleProfileInterval = profileInt.TotalMilliseconds;
            this.TotalEvents = eventCount;
            this.LostEvents = lostEvents;
            this.FileSize = filesize / 1024.0 / 1024.0;
        }

        public string MachineName { get; }

        public string OperatingSystemName { get; }

        public string OperatingSystemBuildNumber { get; }

        public int? UTCDiff { get; }

        public double UTCOffsetCurrentProcess { get; }

        public DateTime BootTime { get; }

        public DateTime StartTime { get; }

        public DateTime EndTime { get; }

        public string EndTimeRelativeMSec { get; }

        public double Duration { get; }

        public int ProcessorSpeed { get; }

        public int NumberOfProcessors { get; }

        public int MemorySize { get; }

        public int PointerSize { get; }

        public double SampleProfileInterval { get; }

        public int TotalEvents { get; }

        public int LostEvents { get; }

        public double FileSize { get; }
    }
}
