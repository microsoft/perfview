﻿using Microsoft.Diagnostics.Tracing.StackSources;
using Microsoft.Diagnostics.Tracing.Stacks;
using PerfView.TestUtilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using System.Linq.Expressions;
using System;

namespace LinuxTracingTests
{
    public class EventParseTests
    {
        private void DoStackTraceTest(string path, bool doBlockedTime, List<List<string>> callerStacks)
        {
            ParallelLinuxPerfScriptStackSource stackSource = new ParallelLinuxPerfScriptStackSource(path, doBlockedTime);

            for (int i = 0; i < stackSource.SampleIndexLimit; i++)
            {
                var sample = stackSource.GetSampleByIndex((StackSourceSampleIndex)i);

                var stackIndex = sample.StackIndex;
                for (int j = 0; (int)stackIndex != -1; j++)
                {
                    var frameIndex = stackSource.GetFrameIndex(stackIndex);
                    Assert.Equal(callerStacks[i][j], stackSource.GetFrameName(frameIndex, false));
                    stackIndex = stackSource.GetCallerIndex(stackIndex);
                }

                Assert.Equal(-1, (int)stackIndex);

                var linuxsample = stackSource.GetLinuxPerfScriptSampleByIndex((StackSourceSampleIndex)i);
                Assert.NotNull(linuxsample);
                Assert.NotEqual(StackSourceSampleIndex.Invalid, linuxsample.SampleIndex);
            }
        }

        private void HeaderTest(string source, bool blockedTime,
            string[] commands,
            int[] pids,
            int[] tids,
            int[] cpus,
            double[] times,
            int[] timeProperties,
            string[] events,
            string[] eventProperties,
            EventKind[] eventKinds,
            ScheduleSwitch[] switches
            )
        {
            using (Stream stream = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                LinuxPerfScriptEventParser parser = new LinuxPerfScriptEventParser();
                List<LinuxEvent> samples = parser.ParseSkippingPreamble(stream).ToList();

                // Need to make sure we have the same amount of samples
                Assert.Equal(commands.Length, parser.EventCount);

                int schedCount = 0;

                for (int i = 0; i < parser.EventCount; i++)
                {
                    LinuxEvent linuxEvent = samples[i];
                    Assert.Equal(commands[i], linuxEvent.Command);
                    Assert.Equal(pids[i], linuxEvent.ProcessID);
                    Assert.Equal(tids[i], linuxEvent.ThreadID);
                    Assert.Equal(cpus[i], linuxEvent.CpuNumber);
                    Assert.True(Math.Abs(times[i] - linuxEvent.TimeMSec) <= 0.0001);
                    Assert.Equal(timeProperties[i], linuxEvent.TimeProperty);
                    Assert.Equal(events[i], linuxEvent.EventName);
                    Assert.Equal(eventProperties[i], linuxEvent.EventProperty);
                    Assert.Equal(eventKinds == null ? EventKind.Cpu : eventKinds[i], linuxEvent.Kind);

                    SchedulerEvent sched = linuxEvent as SchedulerEvent;
                    if (switches != null && sched != null)
                    {
                        ScheduleSwitch actualSwitch = sched.Switch;
                        ScheduleSwitch expectedSwitch = switches[schedCount++];
                        Assert.Equal(expectedSwitch.NextCommand, actualSwitch.NextCommand);
                        Assert.Equal(expectedSwitch.NextPriority, actualSwitch.NextPriority);
                        Assert.Equal(expectedSwitch.NextThreadID, actualSwitch.NextThreadID);
                        Assert.Equal(expectedSwitch.PreviousCommand, actualSwitch.PreviousCommand);
                        Assert.Equal(expectedSwitch.PreviousPriority, actualSwitch.PreviousPriority);
                        Assert.Equal(expectedSwitch.PreviousState, actualSwitch.PreviousState);
                        Assert.Equal(expectedSwitch.PreviousThreadID, actualSwitch.PreviousThreadID);

                    }
                }
            }
        }

        [Fact]
        public void OneStack()
        {
            string path = Constants.GetTestingPerfDumpPath("onegeneric");
            DoStackTraceTest(path, doBlockedTime: false, callerStacks: new List<List<string>> {
                new List<string>{ "module!symbol", "Thread (0)", "Process comm (0)", null }
            });
        }

        [Fact]
        [UseCulture("fr-FR")]
        public void OneStackButWithAnotherCulture()
        {
            string path = Constants.GetTestingPerfDumpPath("onegeneric");
            DoStackTraceTest(path, doBlockedTime: false, callerStacks: new List<List<string>> {
                new List<string>{ "module!symbol", "Thread (0)", "Process comm (0)", null }
            });
        }

        [Fact(Skip = "FIX NOW")]
        public void ResolvingSymbols()
        {
            string path = Constants.GetTestingFilePath("symbol-tests.trace.zip");
            DoStackTraceTest(path, doBlockedTime: false, callerStacks: new List<List<string>>
            {
                new List<string> { "mscorlib!dynamicClass::IL_STUB_PInvoke(int32,native int,int32,int32,class System.Text.StringBuilder,int32,native int)", "Thread (0)", "first_symbol", null },
                new List<string> { "mscorlib!System.BadImageFormatException::.ctor(string,string)", "Thread (0)", "middle_symbol", null },
                new List<string> { "mscorlib!System.Collections.Generic.Dictionary`2+Enumerator[System.UInt64,System.__Canon]::.ctor(class System.Collections.Generic.Dictionary`2<!0,!1>,int32)", "Thread (0)", "last_symbol", null },
                new List<string> { "mscorlib.ni.dll!unknown", "Thread (0)", "out_of_range", null }
            });
        }

        [Fact]
        public void LargeStack()
        {
            string path = Constants.GetTestingPerfDumpPath("two_small_generic");
            DoStackTraceTest(path, doBlockedTime: false, callerStacks: new List<List<string>>
            {
                new List<string> { "module!symbol", "module2!symbol2", "main!main", "Thread (0)", "Process comm (0)" },
                new List<string> { "module3!symbol3", "module4!symbol4", "main!main", "Thread (1)", "Process comm2 (1)" }
            });
        }

        [Fact]
        public void MicrosoftStackTrace()
        {
            string path = Constants.GetTestingPerfDumpPath("ms_stack");
            DoStackTraceTest(path, doBlockedTime: false, callerStacks: new List<List<string>>
            {
                new List<string> { "module!symbol(param[])", "Thread (0)", "Process comm (0)" },
            });
        }

        [Fact]
        public void SchedStackTrace()
        {
            string path = Constants.GetTestingPerfDumpPath("one_complete_switch");
            DoStackTraceTest(path, doBlockedTime: true, callerStacks: new List<List<string>>
            {
                new List<string> { "BLOCKED_TIME", "module!symbol", "Thread (0)", "Process comm1 (0)"},
                new List<string> { "BLOCKED_TIME", "module!symbol", "Thread (1)", "Process comm2 (1)"},
            });
        }

        [Fact]
        public void EmptyStackFrames()
        {
            string path = Constants.GetTestingPerfDumpPath("no_stack_frames");
            DoStackTraceTest(path, doBlockedTime: false,
                callerStacks: new List<List<string>>
                {
                    new List<string> { "Thread (0)", "Process comm (0)" },
                    new List<string> { "module!symbol", "Thread (0)", "Process comm (0)" },
                });
        }

        [Fact]
        public void NonSchedHeader()
        {
            string path = Constants.GetTestingPerfDumpPath("onegeneric");
            HeaderTest(path, blockedTime: false,
                commands: new string[] { "comm" },
                pids: new int[] { 0 },
                tids: new int[] { 0 },
                cpus: new int[] { 0 },
                times: new double[] { 0.0 },
                timeProperties: new int[] { 1 },
                events: new string[] { "event_name" },
                eventProperties: new string[] { "event_properties" },
                eventKinds: null,
                switches: null);
        }

        [Fact]
        public void SpaceSeparatedCommandHeader()
        {
            string path = Constants.GetTestingPerfDumpPath("space_sep_command");
            HeaderTest(path, blockedTime: false,
                commands: new string[] { "comm and3 another part to the command line5 part3 part1" },
                pids: new int[] { 0 },
                tids: new int[] { 0 },
                cpus: new int[] { 0 },
                times: new double[] { 0.0 },
                timeProperties: new int[] { 1 },
                events: new string[] { "event_name" },
                eventProperties: new string[] { "event_properties" },
                eventKinds: null,
                switches: null);
        }

        [Fact]
        public void SchedHeaderFormat1()
        {
            string path = Constants.GetTestingPerfDumpPath("one_complete_switch");
            HeaderTest(path, blockedTime: true,
                commands: new string[] { "comm1", "comm2" },
                pids: new int[] { 0, 1 },
                tids: new int[] { 0, 1 },
                cpus: new int[] { 0, 1 },
                times: new double[] { 0.0, 1000.0 },
                timeProperties: new int[] { 1, 1 },
                events: new string[] { "sched", "sched" },
                eventProperties: new string[] { "sched_switch: prev_comm=comm1 prev_pid=0 prev_prio=0 prev_state=S ==> next_comm=comm2 next_pid=1 next_prio=1", "sched_switch: prev_comm=comm2 prev_pid=1 prev_prio=0 prev_state=S ==> next_comm=comm1 next_pid=0 next_prio=1" },
                eventKinds: new EventKind[] { EventKind.Scheduler, EventKind.Scheduler },
                switches: new ScheduleSwitch[]
                {
                    new ScheduleSwitch("comm1", 0, 0, 'S', "comm2", 1, 1),
                    new ScheduleSwitch("comm2", 1, 0, 'S', "comm1", 0, 1)
                });
        }

        [Fact]
        public void SchedHeaderFormat2()
        {
            string path = Constants.GetTestingPerfDumpPath("one_complete_switch_format2");
            HeaderTest(path, blockedTime: true,
                commands: new string[] { "comm1", "comm2" },
                pids: new int[] { 0, 1 },
                tids: new int[] { 0, 1 },
                cpus: new int[] { 0, 1 },
                times: new double[] { 0.0, 1000.0 },
                timeProperties: new int[] { 1, 1 },
                events: new string[] { "sched", "sched" },
                eventProperties: new string[] { "sched_switch: comm1:0 [0] S ==> comm2:1 [1]", "sched_switch: comm2:1 [0] S ==> comm1:0 [1]" },
                eventKinds: new EventKind[] { EventKind.Scheduler, EventKind.Scheduler },
                switches: new ScheduleSwitch[]
                {
                    new ScheduleSwitch("comm1", 0, 0, 'S', "comm2", 1, 1),
                    new ScheduleSwitch("comm2", 1, 0, 'S', "comm1", 0, 1)
                });
        }

        [Fact]
        public void CornerCaseProcessNames()
        {
            string path = Constants.GetTestingPerfDumpPath("corner_case_process_names");
            HeaderTest(path, blockedTime: false,
                commands: new string[] { "6", "77", "", "k/2", "k/[2]", "k:2"},
                pids: new int[] { 0, 1, 2, 3, 4, 5 },
                tids: new int[] { 0, 1, 2, 3, 4, 5 },
                cpus: new int[] { 0, 0, 0, 0, 0, 0 },
                times: new double[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 },
                timeProperties: new int[] { 1, 1, 1, 1, 1, 1 },
                events: new string[] { "event_name", "event_name", "event_name", "event_name", "event_name", "event_name" },
                eventProperties: new string[] { "event_properties", "event_properties", "event_properties", "event_properties", "event_properties", "event_properties" },
                eventKinds: null,
                switches: null);
        }

        [Fact]
        public void DiskIoNoStacks()
        {
            string path = Constants.GetTestingPerfDumpPath("disk_io_no_stacks");
            HeaderTest(path, blockedTime: false,
                commands: new string[] { "fio", "swapper", "fio", "swapper" },
                pids: new int[] { 236193, 0, 236193, 0 },
                tids: new int[] { 236193, 0, 236193, 0 },
                cpus: new int[] { 21, 12, 21, 12 },
                times: new double[] { 1791.544, 1791.615, 1791.628, 1791.699 },
                timeProperties: new int[] { 1, 1, 1, 1 },
                events: new string[] { "block", "block", "block", "block" },
                eventProperties: new string[] { "block_rq_issue: 259,76 R 4096 () 57622160 + 8 [fio] ffffffff8beead90 blk_mq_start_request ([kernel.kallsyms])", "block_rq_complete: 259,76 R () 57622160 + 8 [0] ffffffff8bee2b4c blk_update_request ([kernel.kallsyms])", "block_rq_issue: 259,76 R 4096 () 82612968 + 8 [fio] ffffffff8beead90 blk_mq_start_request ([kernel.kallsyms])", "block_rq_complete: 259,76 R () 82612968 + 8 [0] ffffffff8bee2b4c blk_update_request ([kernel.kallsyms])" },
                eventKinds: new EventKind[] { EventKind.BlockRequestIssue, EventKind.BlockRequestComplete, EventKind.BlockRequestIssue, EventKind.BlockRequestComplete },
                switches: null);
        }

        [Fact]
        public void OneWakeup()
        {
            string path = Constants.GetTestingPerfDumpPath("one_wakeup");
            HeaderTest(path, blockedTime: false,
                commands: new string[] { "swapper", "swapper" },
                pids: new int[] { 0, 0 },
                tids: new int[] { 0, 0 },
                cpus: new int[] { 9, 9 },
                times: new double[] { 0.0, 0.0 },
                timeProperties: new int[] { 1, 1 },
                events: new string[] { "sched", "sched" },
                eventProperties: new string[] { "sched_wakeup: comm=fio pid=243615 prio=120 target_cpu=031 ffffffff8bad311d ttwu_do_wakeup ([kernel.kallsyms])", "sched_wakeup: task fio:243615 [120] success=1 [031] ffffffff8bad311d ttwu_do_wakeup ([kernel.kallsyms])" },
                eventKinds: new EventKind[] { EventKind.Wakeup, EventKind.Wakeup },
                switches: null);
        }

        [Fact]
        public void ExecProcess()
        {
            string path = Constants.GetTestingPerfDumpPath("exec_process");
            HeaderTest(path, blockedTime: false,
                commands: new string[] { "probe-bcache" },
                pids: new int[] { 286053, 286053 },
                tids: new int[] { 286053, 286053 },
                cpus: new int[] { 3, 3 },
                times: new double[] { 0.0, 0.0 },
                timeProperties: new int[] { 1, 1 },
                events: new string[] { "sched" },
                eventProperties: new string[] { "sched_process_exec: filename=/lib/udev/probe-bcache pid=286053 old_pid=286053" },
                eventKinds: new EventKind[] { EventKind.ProcessExec },
                switches: null);
        }
    }
}
