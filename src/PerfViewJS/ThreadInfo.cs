// <copyright file="ThreadInfo.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System;

    public sealed class ThreadInfo
    {
        public ThreadInfo(int threadId, int threadIndex, double cpumsec, DateTime startTime, double startTimeRelativeMSec, DateTime endTime, double endTimeRelativeMSec)
        {
            this.ThreadId = threadId;
            this.ThreadIndex = threadIndex;
            this.CPUMsec = cpumsec;
            this.StartTime = startTime;
            this.StartTimeRelativeMSec = startTimeRelativeMSec;
            this.EndTime = endTime;
            this.EndTimeRelativeMSec = endTimeRelativeMSec;
        }

        public int ThreadId { get; }

        public int ThreadIndex { get; }

        public DateTime StartTime { get; }

        public double StartTimeRelativeMSec { get; }

        public DateTime EndTime { get; }

        public double EndTimeRelativeMSec { get; }

        public double CPUMsec { get; }
    }
}
