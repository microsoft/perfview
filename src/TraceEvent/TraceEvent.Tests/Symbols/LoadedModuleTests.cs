// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Xunit;

namespace TraceEventTests
{
    public class LoadedModuleTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SamePathReadyToRun_PersistsNativeAssociationAndDistinctPdbs(bool managedFirst)
        {
            WithTrace(managedFirst, includeNative: true, log =>
            {
                var modules = log.Processes.Single(p => p.ProcessID == ProcessId).LoadedModules;
                var managed = Assert.IsType<TraceManagedModule>(Assert.Single(modules.Where(m => m is TraceManagedModule)));
                var native = Assert.Single(modules.Where(m => !(m is TraceManagedModule)));

                Assert.Same(native, managed.NativeModule);
                Assert.Same(managed.ModuleFile, native.ModuleFile.ManagedModule);
                Assert.NotSame(managed.ModuleFile, native.ModuleFile);
                Assert.Equal(managed.ModuleFile.FilePath, native.ModuleFile.FilePath);
                Assert.Equal(ManagedPdb, managed.ModuleFile.PdbSignature);
                Assert.Equal(NativePdb, native.ModuleFile.PdbSignature);
                Assert.Equal(1, managed.ModuleFile.PdbAge);
                Assert.Equal(2, native.ModuleFile.PdbAge);
                Assert.True(native.ModuleFile.PEInfo.IsReadyToRun);
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void LookupKind_FiltersBothTimeOverloadsAndPreservesUnfilteredOrder(bool managedFirst)
        {
            WithTrace(managedFirst, includeNative: true, log =>
            {
                var modules = log.Processes.Single(p => p.ProcessID == ProcessId).LoadedModules;
                var managed = Assert.IsType<TraceManagedModule>(Assert.Single(modules.Where(m => m is TraceManagedModule)));
                var native = Assert.Single(modules.Where(m => !(m is TraceManagedModule)));
                long qpc = StartQpc + 30;
                double msec = log.QPCTimeToRelMSec(qpc);
                string path = ModulePath.ToUpperInvariant();
                TraceLoadedModule first = managedFirst ? managed : native;

                Assert.Same(first, modules.GetLoadedModule(path, qpc));
                Assert.Same(first, modules.GetLoadedModule(path, msec));
                AssertLookup(log, TraceLoadedModuleKind.Any, qpc, first);
                AssertLookup(log, TraceLoadedModuleKind.Native, qpc, native);
                AssertLookup(log, TraceLoadedModuleKind.Managed, qpc, managed);
                AssertLookup(log, TraceLoadedModuleKind.Native, StartQpc + 9, null);
                AssertLookup(log, TraceLoadedModuleKind.Native, StartQpc + 10, native);
                AssertLookup(log, TraceLoadedModuleKind.Managed, StartQpc + 59, managed);
                AssertLookup(log, TraceLoadedModuleKind.Managed, StartQpc + 60, null);
                AssertLookup(log, TraceLoadedModuleKind.Native, StartQpc + 60, native);
                AssertLookup(log, TraceLoadedModuleKind.Native, StartQpc + 70, null);
                Assert.Null(modules.GetLoadedModule(@"C:\runtime\missing.dll", qpc, TraceLoadedModuleKind.Any));
                Assert.Null(modules.GetLoadedModule(@"C:\runtime\missing.dll", msec, TraceLoadedModuleKind.Any));
            });
        }

        [Fact]
        public void ReadyToRun_WithoutNativeMapping_DoesNotLinkManagedModuleToItself()
        {
            WithTrace(managedFirst: true, includeNative: false, log =>
            {
                var modules = log.Processes.Single(p => p.ProcessID == ProcessId).LoadedModules;
                var managed = Assert.IsType<TraceManagedModule>(Assert.Single(modules.Where(m => m is TraceManagedModule)));
                Assert.Null(managed.NativeModule);
                Assert.Null(managed.ModuleFile.ManagedModule);
                AssertLookup(log, TraceLoadedModuleKind.Any, StartQpc + 30, managed);
                AssertLookup(log, TraceLoadedModuleKind.Managed, StartQpc + 30, managed);
                AssertLookup(log, TraceLoadedModuleKind.Native, StartQpc + 30, null);
            });
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        public void LookupKind_RejectsInvalidSelection(int kind)
        {
            WithTrace(managedFirst: true, includeNative: true, log =>
            {
                var modules = log.Processes.Single(p => p.ProcessID == ProcessId).LoadedModules;
                Assert.Throws<ArgumentOutOfRangeException>("moduleKind", () =>
                    modules.GetLoadedModule(ModulePath, StartQpc + 30, (TraceLoadedModuleKind)kind));
                Assert.Throws<ArgumentOutOfRangeException>("moduleKind", () =>
                    modules.GetLoadedModule(ModulePath, log.QPCTimeToRelMSec(StartQpc + 30), (TraceLoadedModuleKind)kind));
            });
        }

        #region private

        private static void AssertLookup(TraceLog log, TraceLoadedModuleKind kind, long qpc, TraceLoadedModule expected)
        {
            var modules = log.Processes.Single(p => p.ProcessID == ProcessId).LoadedModules;
            Assert.Same(expected, modules.GetLoadedModule(ModulePath.ToUpperInvariant(), qpc, kind));
            Assert.Same(expected, modules.GetLoadedModule(ModulePath.ToUpperInvariant(), log.QPCTimeToRelMSec(qpc), kind));
        }

        private static void WithTrace(bool managedFirst, bool includeNative, Action<TraceLog> verify) =>
            SyntheticEtwSource.ConvertAndVerify(() => new ModuleEventSource(managedFirst, includeNative), verify);

        private unsafe sealed class ModuleEventSource : SyntheticEtwSource
        {
            public ModuleEventSource(bool managedFirst, bool includeNative) : base(StartQpc, ProcessId)
            {
                m_moduleId = managedFirst ? ImageBase - 0x10000 : ImageBase + 0x10000;
                m_includeNative = includeNative;
            }

            public override bool Process()
            {
                if (m_includeNative) { Image(StartQpc + 10, 10); }
                Managed(StartQpc + 20, 152, 33);
                Managed(StartQpc + 60, 153, 34);
                if (m_includeNative) { Image(StartQpc + 70, 2); }
                return true;
            }

            private void Image(long qpc, byte opcode)
            {
                Emit(KernelTraceEventParser.ImageTaskGuid, 0, opcode, qpc, classic: true, write: w =>
                {
                    w.Write(ImageBase);
                    w.Write(0x10000UL);
                    w.Write(ProcessId);
                    w.Write(new byte[12]);
                    w.Write(ImageBase);
                    w.Write(new byte[16]);
                    WriteString(w, ModulePath);
                });
            }

            private void Managed(long qpc, ushort id, byte opcode)
            {
                Emit(ClrTraceEventParser.ProviderGuid, id, opcode, qpc, classic: false, write: w =>
                {
                    w.Write(m_moduleId);
                    w.Write(0xBABEL);
                    w.Write((int)(ModuleFlags.Manifest | ModuleFlags.ReadyToRunModule));
                    w.Write(0);
                    WriteString(w, ModulePath);
                    WriteString(w, "");
                    w.Write((ushort)1);
                    w.Write(ManagedPdb.ToByteArray());
                    w.Write(1);
                    WriteString(w, "System.Private.CoreLib.pdb");
                    w.Write(NativePdb.ToByteArray());
                    w.Write(2);
                    WriteString(w, "System.Private.CoreLib.ni.pdb");
                });
            }

            private void Emit(Guid provider, ushort id, byte opcode, long qpc, bool classic, Action<BinaryWriter> write) =>
                Emit(provider, id, opcode, qpc, threadId: 5000, classic, Payload(write));

            private static void WriteString(BinaryWriter writer, string value)
            {
                writer.Write(Encoding.Unicode.GetBytes(value));
                writer.Write((ushort)0);
            }

            private readonly ulong m_moduleId;
            private readonly bool m_includeNative;
        }

        private const int ProcessId = 1234;
        private const long StartQpc = 100_000;
        private const ulong ImageBase = 0x200000;
        private const string ModulePath = @"C:\runtime\System.Private.CoreLib.dll";
        private static readonly Guid ManagedPdb = new Guid("b6b2c395-5814-2d32-1a69-05d71e8d5b21");
        private static readonly Guid NativePdb = new Guid("bf980aa4-0c4e-1606-4356-56ae9a3ff5b6");

        #endregion
    }
}
