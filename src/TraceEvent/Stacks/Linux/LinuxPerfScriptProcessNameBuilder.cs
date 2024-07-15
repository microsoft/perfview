using Microsoft.Diagnostics.Tracing.Stacks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.Diagnostics.Tracing.StackSources
{
    internal sealed class LinuxPerfScriptProcessNameBuilder
    {
        private static readonly string[] IgnoredNames = new string[]
            {
                ".NET Finalizer",
                ".NET Tiered Compilation Worker",
                ".NET BGC",
                ".NET Server GC"
            };

        private readonly object _dictionariesLock = new object();
        private Dictionary<StackSourceFrameIndex, HashSet<string>> _candidateProcessNames = new Dictionary<StackSourceFrameIndex, HashSet<string>>();
        private Dictionary<StackSourceFrameIndex, string> _cachedProcessNames = new Dictionary<StackSourceFrameIndex, string>();
        private Dictionary<StackSourceFrameIndex, int> _processIds = new Dictionary<StackSourceFrameIndex, int>();

        internal void SaveProcessName(StackSourceFrameIndex frameIndex, string processName, int processId)
        {
            lock (_dictionariesLock)
            {
                if (!_candidateProcessNames.TryGetValue(frameIndex, out HashSet<string> processNames))
                {
                    processNames = new HashSet<string>();
                    _candidateProcessNames.Add(frameIndex, processNames);
                }

                processNames.Add(processName);

                _processIds[frameIndex] = processId;
            }
        }

        internal string GetProcessName(StackSourceFrameIndex frameIndex)
        {
            lock (_dictionariesLock)
            {
                if (!_cachedProcessNames.TryGetValue(frameIndex, out string processName))
                {
                    processName = BuildProcessName(frameIndex);
                    _cachedProcessNames.Add(frameIndex, processName);
                }

                return processName;
            }
        }

        private string BuildProcessName(StackSourceFrameIndex frameIndex)
        {
            Debug.Assert(Monitor.IsEntered(_dictionariesLock));

            if (_candidateProcessNames.TryGetValue(frameIndex, out HashSet<string> processNames))
            {
                int processId = _processIds[frameIndex];

                string[] names = processNames.ToArray();
                if (names.Length == 0)
                {
                    Debug.Assert(false);
                    return $"Process Unknown (0)";
                }
                else if (names.Length == 1)
                {
                    return $"Process {names[0]} ({processId})";
                }
                else
                {
                    bool addDelimeter = false;
                    StringBuilder builder = new StringBuilder();
                    builder.Append("Process ");

                    // Try to build a process name that doesn't include the ignored frames.
                    for (int i = 0; i < names.Length; i++)
                    {
                        bool skip = false;
                        foreach (string ignoredFrame in IgnoredNames)
                        {
                            if (ignoredFrame.Equals(names[i]))
                            {
                                skip = true;
                                break;
                            }
                        }

                        if (skip)
                        {
                            continue;
                        }

                        if (addDelimeter)
                        {
                            builder.Append(";");
                        }

                        builder.Append(names[i]);
                        addDelimeter = true;
                    }

                    if (!addDelimeter)
                    {
                        // If we threw away all of the possible process names, don't ignore them
                        // and just build a process name with all available options.
                        for (int i = 0; i < names.Length; i++)
                        {
                            if (addDelimeter)
                            {
                                builder.Append(";");
                            }

                            builder.Append(names[i]);
                            addDelimeter = true;
                        }
                    }
                    builder.Append($" ({processId})");

                    return builder.ToString();
                }
            }
            else
            {
                Debug.Assert(false);
                return "Process Unknown (0)";
            }
        }
    }
}
