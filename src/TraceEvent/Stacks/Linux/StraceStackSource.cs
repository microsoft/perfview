using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Tracing.Utilities;

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

        private double _currentTimeStampInMs = 0;
        private StraceRecord _currentRecord = new StraceRecord();
        private StackSourceSample _sample;

        public StraceStackSource(string path)
        {
            _sample = new StackSourceSample(this);

            using (StreamReader reader = new StreamReader(path))
            {
                //FastStream fastStream = new FastStream(stream, BufferSize);
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
                    ProcessRecord(recordBuilder);
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

            // Update the current record.
            _currentRecord.TimeStampRelativeMs = _currentTimeStampInMs;
            _currentRecord.SyscallName = name;
            _currentRecord.LatencyInMilliseconds = latency * 1000;

            DispatchRecord(_currentRecord);
        }

        private void DispatchRecord(StraceRecord record)
        {
            StackSourceFrameIndex frameIndex = Interner.FrameIntern($"Syscall: {record.SyscallName}");
            _sample.StackIndex = Interner.CallStackIntern(frameIndex, StackSourceCallStackIndex.Invalid);
            _sample.TimeRelativeMSec = record.TimeStampRelativeMs;
            _sample.Metric = (float) record.LatencyInMilliseconds;
            AddSample(_sample);
        }
    }

    internal sealed class StraceRecord
    {
        public double TimeStampRelativeMs { get; set; }
        public string SyscallName { get; set; }
        public double LatencyInMilliseconds { get; set; }
    }
}
