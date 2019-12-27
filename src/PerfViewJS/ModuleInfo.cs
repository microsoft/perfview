// <copyright file="ModuleInfo.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System;

    public sealed class ModuleInfo : IComparable<ModuleInfo>
    {
        public ModuleInfo(int moduleIndex, int addressesInModule, string modulePath)
        {
            this.Id = moduleIndex;
            this.AddrCount = addressesInModule;
            this.ModulePath = modulePath;
        }

        public int Id { get; }

        public int AddrCount { get; }

        public string ModulePath { get; }

        public int CompareTo(ModuleInfo other)
        {
            if (this.AddrCount > other.AddrCount)
            {
                return -1;
            }
            else if (this.AddrCount < other.AddrCount)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
    }
}
