// <copyright file="ProcessInfo.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    public sealed class ProcessInfo
    {
        public ProcessInfo(string processName, int index, float cpumsec, int processId, int parentid, string commandline)
        {
            this.Name = processName;
            this.Id = index;
            this.ProcessId = processId;
            this.CPUMSec = cpumsec;
            this.ParentId = parentid;
            this.CommandLine = commandline;
        }

        public string Name { get; }

        public int Id { get; }

        public int ProcessId { get; }

        public int ParentId { get; }

        public string CommandLine { get; }

        public float CPUMSec { get; }
    }
}
