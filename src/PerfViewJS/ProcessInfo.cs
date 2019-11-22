// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    public sealed class ProcessInfo
    {
        public ProcessInfo(string processName, int pid, float cpumsec)
        {
            this.Name = processName;
            this.Id = pid;
            this.CPUMSec = cpumsec;
        }

        public string Name { get; }

        public int Id { get; }

        public float CPUMSec { get; }
    }
}
