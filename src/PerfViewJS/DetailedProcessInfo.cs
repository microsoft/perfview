// <copyright file="DetailedProcessInfo.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System.Collections.Generic;

    public sealed class DetailedProcessInfo
    {
        public DetailedProcessInfo(ProcessInfo processInfo, List<ThreadInfo> threads, List<ModuleInfo> modules)
        {
            this.ProcessInfo = processInfo;
            this.Threads = threads;
            this.Modules = modules;
        }

        public ProcessInfo ProcessInfo { get; }

        public List<ThreadInfo> Threads { get; }

        public List<ModuleInfo> Modules { get; }
    }
}
