using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.StackSources
{
    /// <summary>
    /// Parses strace files that were collected with the following command line:
    /// 
    ///     strace -r -T
    ///     
    /// -r: Print's the relative time stamp for each entry (relative to the previous entry).
    /// -T: Shows the time spent in syscalls.
    /// </summary>
    public class StraceStackSource : InternStackSource
    {
        private const char LatencyStartChar = '<';
        private const char LatencyEndChar = '>';
        private const char ArgumentsStartChar = '(';
        private const char ArgumentsEndChar = ')';
        private const char ReturnCodePrefixChar = '=';

        private double _currentTimeStampInMs = 0;
        private StraceRecord _currentRecord = new StraceRecord();

        private StraceRecordHandlerFactory _handlerFactory;

        public StraceStackSource(string path)
        {
            _handlerFactory = new StraceRecordHandlerFactory(this);

            using (StreamReader reader = new StreamReader(path))
            {
                ProcessStream(reader);
                Interner.DoneInterning();
            }
        }

        private void ProcessStream(StreamReader reader)
        {
            // Read one full entry.  Most entries only take up a single line.
            // Some can be multiple lines.
            // Look for the latency timestamp wrapped in '<' and '>' to signify the end of the entry.
            StringBuilder recordBuilder = new StringBuilder();
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                recordBuilder.Append(line);

                if (ContainsEndOfRecord(line))
                {
                    try
                    {
                        ProcessRecord(recordBuilder);
                    }
                    catch
                    {
                        // Skip and allow for processing of the next record.
                    }

                    recordBuilder.Clear();
                }
            }
        }

        private bool ContainsEndOfRecord(string line)
        {
            // Check for end of entry latency timestamp.
            int startCharIndex = line.LastIndexOf(LatencyStartChar);
            int endCharIndex = line.LastIndexOf(LatencyEndChar);
            
            if (startCharIndex == -1 || endCharIndex == -1)
            {
                return false;
            }

            if (endCharIndex != line.Length - 1)
            {
                return false;
            }

            for(int i=startCharIndex + 1; i<endCharIndex; i++)
            {
                if (!char.IsDigit(line[i]) && line[i] != '.')
                {
                    return false;
                }    
            }

            return true;
        }

        private void ProcessRecord(StringBuilder recordBuilder)
        {
            int cur = 0;
            string record = recordBuilder.ToString();

            // Skip lines that start with "strace:"
            if (record.StartsWith("strace:"))
            {
                return;
            }

            // Skip whitespace at the beginning of the record.
            while (cur < record.Length && char.IsWhiteSpace(record[cur]))
            {
                cur++;
            }

            // Check for malformed record.
            if (cur == record.Length - 1)
            {
                return;
            }

            // Walk forward to get the get the relative time stamp.
            int startIndex = cur;
            int endIndex = startIndex;
            while(cur < record.Length && (char.IsDigit(record[cur]) || record[cur] == '.'))
            {
                cur++;
                endIndex = cur;
            }

            string relTimeStampStr = record.Substring(startIndex, endIndex - startIndex);
            double relTimeStamp = double.Parse(relTimeStampStr);
            
            // Update the current timestamp.
            _currentTimeStampInMs += relTimeStamp * 1000;

            // Walk forward to get the syscall name.
            while (char.IsWhiteSpace(record[cur]))
            {
                cur++;
            }

            startIndex = cur;
            while (record[cur] != ArgumentsStartChar)
            {
                cur++;
                endIndex = cur;
            }

            string name = record.Substring(startIndex, endIndex - startIndex);

            // Walk backwards to get the latency.
            Debug.Assert(record[record.Length - 1] == LatencyEndChar);

            endIndex = record.Length - 1;
            startIndex = endIndex - 1;
            while (record[startIndex] != LatencyStartChar)
            {
                startIndex--;
            }

            string latencyStr = record.Substring(startIndex + 1, endIndex - startIndex - 1);
            double latency = double.Parse(latencyStr);

            // Walk backwards to get the return code.
            endIndex = startIndex;
            int parenthesisLevel = 0;
            while (startIndex >= 0)
            {
                char currentChar = record[startIndex];
                if (currentChar == ReturnCodePrefixChar && parenthesisLevel == 0)
                {
                    break;
                }
                else if (currentChar == ')')
                {
                    parenthesisLevel++;
                }
                else if (currentChar == '(')
                {
                    parenthesisLevel--;
                    Debug.Assert(parenthesisLevel >= 0);
                }
                startIndex--;
            }

            string returnCode = record.Substring(startIndex + 1, endIndex - startIndex - 1);
            returnCode = returnCode.Trim();

            // Capture the arguments.
            string argumentPayload = record.Substring(cur, startIndex - cur - 1);
            argumentPayload = argumentPayload.Trim();

            // Confirm that the argument payload starts and ends with parenthesis, and then strip them off.
            Debug.Assert(argumentPayload[0] == ArgumentsStartChar && argumentPayload[argumentPayload.Length - 1] == ArgumentsEndChar);
            argumentPayload = argumentPayload.Substring(1, argumentPayload.Length - 2);

            // Update the current record.
            _currentRecord.SyscallName = name;
            _currentRecord.ArgumentPayload = argumentPayload;
            _currentRecord.ReturnCode = returnCode;
            _currentRecord.TimeStampRelativeMs = _currentTimeStampInMs;
            _currentRecord.LatencyInMilliseconds = latency * 1000;

            DispatchRecord(_currentRecord);
        }

        private void DispatchRecord(StraceRecord record)
        {
            StraceRecordHandler recordHandler = _handlerFactory.GetHandler(record);
            Debug.Assert(recordHandler != null);
            recordHandler.Execute(record);
        }
    }

    internal sealed class StraceRecord
    {
        public string SyscallName { get; set; }
        public string ArgumentPayload { get; set; }
        public string ReturnCode { get; set; }
        public double TimeStampRelativeMs { get; set; }
        public double LatencyInMilliseconds { get; set; }
    }

    internal sealed class StraceRecordHandlerFactory
    {
        private DefaultHandler _defaultHandler;
        private IOHandler _ioHandler;

        public StraceRecordHandlerFactory(StraceStackSource stackSource)
        {
            _defaultHandler = new DefaultHandler(stackSource);
            _ioHandler = new IOHandler(stackSource);
        }

        public StraceRecordHandler GetHandler(StraceRecord record)
        {
            _syscallNames.Add(record.SyscallName);

            switch(record.SyscallName)
            {
                case "openat":
                case "close":
                case "fcntl":
                case "read":
                case "lseek":
                case "pread64":
                case "access":
                case "fstat":
                case "getdents64":
                case "write":
                case "flock":
                case "fadvise64":
                case "ioctl":
                case "lstat":
                case "readlink":
                case "unlink":
                case "mknod":
                case "stat":
                    return _ioHandler;
                default:
                    return _defaultHandler;
            }
        }

        private HashSet<string> _syscallNames = new HashSet<string>();
    }

    internal abstract class StraceRecordHandler
    {
        public StraceRecordHandler(StraceStackSource stackSource)
        {
            StackSource = stackSource;
        }

        protected StraceStackSource StackSource
        {
            get; private set;
        }

        public abstract void Execute(StraceRecord record);
    }

    internal sealed class DefaultHandler : StraceRecordHandler
    {
        private StackSourceSample _sample;

        public DefaultHandler(StraceStackSource stackSource)
            : base(stackSource)
        {
            _sample = new StackSourceSample(stackSource);
        }

        public override void Execute(StraceRecord record)
        {
            // Stack:
            //
            // Syscall
            // |
            //  -Arguments
            //    |
            //     -Return Code

            _sample.StackIndex = StackSourceCallStackIndex.Invalid;

            StackSourceFrameIndex frameIndex = StackSource.Interner.FrameIntern($"Raw Syscalls");
            _sample.StackIndex = StackSource.Interner.CallStackIntern(frameIndex, _sample.StackIndex);
            frameIndex = StackSource.Interner.FrameIntern($"Syscall: {record.SyscallName}");
            _sample.StackIndex = StackSource.Interner.CallStackIntern(frameIndex, _sample.StackIndex);
            frameIndex = StackSource.Interner.FrameIntern($"Arguments: {record.ArgumentPayload}");
            _sample.StackIndex = StackSource.Interner.CallStackIntern(frameIndex, _sample.StackIndex);
            frameIndex = StackSource.Interner.FrameIntern($"Return Code: {record.ReturnCode}");
            _sample.StackIndex = StackSource.Interner.CallStackIntern(frameIndex, _sample.StackIndex);


            _sample.TimeRelativeMSec = record.TimeStampRelativeMs;
            _sample.Metric = (float)record.LatencyInMilliseconds;
            
            StackSource.AddSample(_sample);
        }
    }

    internal sealed class IOHandler : StraceRecordHandler
    {
        private Dictionary<string, string> _fdToPathMap = new Dictionary<string, string>();
        private StackSourceSample _sample;

        public IOHandler(StraceStackSource stackSource)
            :base(stackSource)
        {
            _sample = new StackSourceSample(stackSource);
        }

        public override void Execute(StraceRecord record)
        {
            switch(record.SyscallName)
            {
                case "openat":
                    HandleOpenAtSyscall(record);
                    break;
                case "close":
                    HandleCloseSyscall(record);
                    break;
                case "fcntl":
                    HandleFcntlSyscall(record);
                    break;
                case "read":
                    HandleReadSyscall(record);
                    break;
                case "lseek":
                    HandleLseekSyscall(record);
                    break;
                case "pread64":
                    HandlePread64Syscall(record);
                    break;
                case "access":
                    HandleAccessSyscall(record);
                    break;
                case "fstat":
                    HandleFstatSyscall(record);
                    break;
                case "getdents64":
                    HandleGetdents64Syscall(record);
                    break;
                case "write":
                    HandleWriteSyscall(record);
                    break;
                case "flock":
                    HandleFlockSyscall(record);
                    break;
                case "fadvise64":
                    HandleFadvise64Syscall(record);
                    break;
                case "ioctl":
                    HandleIoctlSyscall(record);
                    break;
                case "lstat":
                    HandleLstatSyscall(record);
                    break;
                case "readlink":
                    HandleReadlinkSyscall(record);
                    break;
                case "unlink":
                    HandleUnlinkSyscall(record);
                    break;
                case "mknod":
                    HandleMknodSyscall(record);
                    break;
                case "stat":
                    HandleStatSyscall(record);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(record.SyscallName));
            }
        }

        private const char ArgumentSeparator = ',';
        private static readonly char[] ArgumentSeparators = new char[]
        {
            ArgumentSeparator,
        };

        private void HandleOpenAtSyscall(StraceRecord record)
        {
            // Get the file path.
            int indexOfFirstComma = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            int indexOfLastComma = record.ArgumentPayload.LastIndexOf(ArgumentSeparator);
            string filePath = record.ArgumentPayload.Substring(
                indexOfFirstComma + 1,
                indexOfLastComma - indexOfFirstComma - 1);
            filePath = filePath.Trim();
            filePath = filePath.Substring(1, filePath.Length - 2);

            // Get the file descriptor.
            string fd = record.ReturnCode;

            // Only save valid file descriptors.
            if (!fd.StartsWith("-1"))
            {
                _fdToPathMap[fd] = filePath;
            }

            AddSample(record, filePath);
        }

        private void HandleCloseSyscall(StraceRecord record)
        {
            string fd = record.ArgumentPayload.Trim();
            _fdToPathMap.TryGetValue(fd, out string filePath);
            _fdToPathMap.Remove(fd);

            AddSample(record, filePath);
        }

        private void HandleFcntlSyscall(StraceRecord record)
        {
            // Split the arguments.
            string[] arguments = record.ArgumentPayload.Split(ArgumentSeparators);
            Debug.Assert(arguments.Length >= 2);
            string fd = arguments[0].Trim();
            string cmd = arguments[1].Trim();

            if (cmd.StartsWith("F_DUPFD"))
            {
                string newfd = record.ReturnCode;
                // Only save valid file descriptors.
                if (!newfd.StartsWith("-1"))
                {
                    // Get the file name from the map.
                    if (_fdToPathMap.TryGetValue(fd, out string path))
                    {
                        // Save the path alongside the new descriptor.
                        _fdToPathMap[newfd] = path;
                    }

                    // TODO: Should we remove the old fd?
                }
            }

            // Get the file path to write with the sample.
            _fdToPathMap.TryGetValue(fd, out string filePath);

            AddSample(record, filePath);
        }

        private void HandleReadSyscall(StraceRecord record)
        {
            // Get the fd.
            int indexOfFirstArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string fd = record.ArgumentPayload.Substring(0, indexOfFirstArgumentSeparator);

            // Get the file name.
            _fdToPathMap.TryGetValue(fd, out string filePath);

            AddSample(record, filePath);
        }

        private void HandleLseekSyscall(StraceRecord record)
        {
            // Get the fd.
            int indexOfFirstArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string fd = record.ArgumentPayload.Substring(0, indexOfFirstArgumentSeparator);

            // Get the file name.
            _fdToPathMap.TryGetValue(fd, out string filePath);

            AddSample(record, filePath);
        }

        private void HandlePread64Syscall(StraceRecord record)
        {
            // Get the fd.
            int indexOfFirstArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string fd = record.ArgumentPayload.Substring(0, indexOfFirstArgumentSeparator);

            // Get the file name.
            _fdToPathMap.TryGetValue(fd, out string filePath);

            AddSample(record, filePath);
        }

        private void HandleAccessSyscall(StraceRecord record)
        {
            // Get the file path.
            int indexOfArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string filePath = record.ArgumentPayload.Substring(1, indexOfArgumentSeparator - 1 - 1); // Strip off the comma and the surrounding quotes.

            AddSample(record, filePath);
        }

        private void HandleFstatSyscall(StraceRecord record)
        {
            // Get the fd.
            int indexOfFirstArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string fd = record.ArgumentPayload.Substring(0, indexOfFirstArgumentSeparator);

            // Get the file name.
            _fdToPathMap.TryGetValue(fd, out string filePath);

            AddSample(record, filePath);
        }

        private void HandleGetdents64Syscall(StraceRecord record)
        {
            // Get the fd.
            int indexOfFirstArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string fd = record.ArgumentPayload.Substring(0, indexOfFirstArgumentSeparator);

            // Get the file name.
            _fdToPathMap.TryGetValue(fd, out string filePath);

            AddSample(record, filePath);
        }

        private void HandleWriteSyscall(StraceRecord record)
        {
            // Get the fd.
            int indexOfFirstArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string fd = record.ArgumentPayload.Substring(0, indexOfFirstArgumentSeparator);

            // Get the file name.
            _fdToPathMap.TryGetValue(fd, out string filePath);

            AddSample(record, filePath);
        }

        private void HandleFlockSyscall(StraceRecord record)
        {
            // Get the fd.
            int indexOfFirstArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string fd = record.ArgumentPayload.Substring(0, indexOfFirstArgumentSeparator);

            // Get the file name.
            _fdToPathMap.TryGetValue(fd, out string filePath);

            AddSample(record, filePath);
        }

        private void HandleFadvise64Syscall(StraceRecord record)
        {
            // Get the fd.
            int indexOfFirstArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string fd = record.ArgumentPayload.Substring(0, indexOfFirstArgumentSeparator);

            // Get the file name.
            _fdToPathMap.TryGetValue(fd, out string filePath);

            AddSample(record, filePath);
        }

        private void HandleIoctlSyscall(StraceRecord record)
        {
            // Get the fd.
            int indexOfFirstArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string fd = record.ArgumentPayload.Substring(0, indexOfFirstArgumentSeparator);

            // Get the file name.
            _fdToPathMap.TryGetValue(fd, out string filePath);

            AddSample(record, filePath);
        }

        private void HandleLstatSyscall(StraceRecord record)
        {
            // Get the file path.
            int indexOfArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string filePath = record.ArgumentPayload.Substring(1, indexOfArgumentSeparator - 1 - 1); // Strip off the comma and the surrounding quotes.

            AddSample(record, filePath);
        }

        private void HandleReadlinkSyscall(StraceRecord record)
        {
            // Get the file path.
            int indexOfArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string filePath = record.ArgumentPayload.Substring(1, indexOfArgumentSeparator - 1 - 1); // Strip off the comma and the surrounding quotes.

            AddSample(record, filePath);
        }

        private void HandleUnlinkSyscall(StraceRecord record)
        {
            // Get the file path.
            string filePath = record.ArgumentPayload.Substring(1, record.ArgumentPayload.Length - 2); // Strip off the comma and the surrounding quotes.

            AddSample(record, filePath);
        }

        private void HandleMknodSyscall(StraceRecord record)
        {
            // Get the file path.
            int indexOfArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string filePath = record.ArgumentPayload.Substring(1, indexOfArgumentSeparator - 1 - 1); // Strip off the comma and the surrounding quotes.

            AddSample(record, filePath);
        }

        private void HandleStatSyscall(StraceRecord record)
        {
            // Get the file path.
            int indexOfArgumentSeparator = record.ArgumentPayload.IndexOf(ArgumentSeparator);
            string filePath = record.ArgumentPayload.Substring(1, indexOfArgumentSeparator - 1 - 1); // Strip off the comma and the surrounding quotes.

            AddSample(record, filePath);
        }

        private void AddSample(StraceRecord record, string filePath)
        {
            // Stack:
            // FilePath
            // |
            //  -Syscall
            //   |
            //    -Arguments
            //     |
            //      -Return Code

            if (string.IsNullOrEmpty(filePath))
            {
                filePath = "<Unknown>";
            }

            _sample.StackIndex = StackSourceCallStackIndex.Invalid;

            StackSourceFrameIndex frameIndex = StackSource.Interner.FrameIntern($"Path: {filePath}");
            _sample.StackIndex = StackSource.Interner.CallStackIntern(frameIndex, _sample.StackIndex);
            frameIndex = StackSource.Interner.FrameIntern($"Syscall: {record.SyscallName}");
            _sample.StackIndex = StackSource.Interner.CallStackIntern(frameIndex, _sample.StackIndex);
            frameIndex = StackSource.Interner.FrameIntern($"Arguments: {record.ArgumentPayload}");
            _sample.StackIndex = StackSource.Interner.CallStackIntern(frameIndex, _sample.StackIndex);
            frameIndex = StackSource.Interner.FrameIntern($"Return Code: {record.ReturnCode}");
            _sample.StackIndex = StackSource.Interner.CallStackIntern(frameIndex, _sample.StackIndex);


            _sample.TimeRelativeMSec = record.TimeStampRelativeMs;
            _sample.Metric = (float)record.LatencyInMilliseconds;

            StackSource.AddSample(_sample);
        }
    }
}
