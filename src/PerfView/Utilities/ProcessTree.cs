using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

namespace PerfView
{
    /// <summary>
    /// A code:ProcessInfo represents a process that existed at the time a snapshot was taken with
    /// the code:ProcessInfo constructor.  It basically is a set of read-only properties describing
    /// the process.   
    /// </summary>
    public class ProcessInfo
    {
        public int ProcessID { get { return processID; } }
        public int ParentProcessID { get { return parentProcessID; } }
        public string CommandLine
        {
            get
            {
                if (commandLine == null)
                {
                    commandLine = (string)processObj["CommandLine"];
                    if (commandLine == null)
                    {
                        commandLine = ExecutablePath;
                    }

                    if (commandLine == null)
                    {
                        commandLine = "";
                    }
                }
                return commandLine;
            }
        }
        public string ExecutablePath
        {
            get
            {
                if (executablePath == null)
                {
                    executablePath = (string)processObj["ExecutablePath"];
                }

                return executablePath;
            }
        }
        public string Name
        {
            get
            {
                if (name == null)
                {
                    name = Path.GetFileNameWithoutExtension(ExecutablePath);
                }
                return name;
            }
        }

        public DateTime CreationDate
        {
            get
            {
                if (creationDate == default(DateTime))
                {
                    string creationDateStr = (string)processObj["CreationDate"];
                    if (creationDateStr != null)
                    {
                        creationDate = ToDateTime(creationDateStr);
                    }
                }
                return creationDate;
            }
        }

        public string ShortDescription
        {
            get
            {
                var shortName = Name;
                if (shortName.Length > 24)
                {
                    shortName = shortName.Substring(0, 24);
                }

                return $"{shortName} ({ProcessID})";
            }
        }

        public long CpuTime100ns
        {
            get
            {
                if (cpuTime100ns == 0)
                {
                    cpuTime100ns = (long)((ulong)processObj["KernelModeTime"] + (ulong)processObj["UserModeTime"]);
                }

                return cpuTime100ns;
            }
        }
        public int PageFaults { get { return (int)(uint)processObj["PageFaults"]; } }
        public long WorkingSetSize { get { return (long)(ulong)processObj["WorkingSetSize"]; } }

        public IList<ProcessInfo> Children { get { return children; } }
        public ProcessInfo Parent { get { return parent; } }
        public override string ToString()
        {
            return ToString("", int.MaxValue);
        }
        public string ToString(string indent, int limit)
        {
            // Strip the EXE name from the command line
            var args = Regex.Replace(CommandLine, "^((\\S+)|(\".*?\"))\\s*", "");
            var shortName = Name;
            if (shortName.Length > 24)
            {
                shortName = shortName.Substring(0, 24);
            }

            return string.Format("{0,-24} | Pid: {1,5} | Alive: {2,6} | Args: {3}", shortName, ProcessID,
                TimeStr(DateTime.Now - CreationDate), args);
        }

        /// <summary>
        /// Returns a time span picking good units so that it can be expressed in a small amount of
        /// space (5 spaces)
        /// </summary>
        /// <param name="span"></param>
        /// <returns></returns>
        public static string TimeStr(TimeSpan span)
        {
            double time = span.TotalMilliseconds;
            if (time < 1000)
            {
                return time.ToString("f0") + "ms";
            }

            time = time / 1000;
            if (time < 60)
            {
                return time.ToString("f1") + "s";
            }

            time = time / 60;
            if (time < 60)
            {
                return time.ToString("f1") + "m";
            }

            time = time / 60;
            if (time < 24)
            {
                return time.ToString("f1") + "h";
            }

            time = time / 24;
            if (time < 100)
            {
                return time.ToString("f1") + "d";
            }

            return time.ToString("f0") + "d";
        }

        #region Private

        internal ProcessInfo(ManagementBaseObject processObj)
        {
            this.processObj = processObj;
            processID = (int)(uint)processObj["ProcessID"];
            parentProcessID = (int)(uint)processObj["ParentProcessID"];
            children = emptyList;
        }

        /// <summary>
        /// Given a command line, compress out uninteresting parts to show the most important part. 
        /// </summary>
        private static string CompressCommandLine(string commandLine, int limit)
        {
            if (commandLine.Length > limit)
            {
                commandLine = Regex.Replace(commandLine, "\"([^\"]*)\"", "$1");
                commandLine = Regex.Replace(commandLine, @"(\S+).exe", "$1", RegexOptions.IgnoreCase);
                commandLine = Regex.Replace(commandLine, @"\S+\\", "");
                commandLine = Regex.Replace(commandLine, @"\s+", " ");
                if (commandLine.Length > limit)
                {
                    commandLine = commandLine.Substring(0, limit - 1) + "...";
                }
            }
            return commandLine;
        }

        // Converts a given datetime in DMTF format to System.DateTime object.
        internal List<ProcessInfo> children;
        internal ProcessInfo parent;
        private static System.DateTime ToDateTime(string dmtfDate)
        {
            System.DateTime initializer = System.DateTime.MinValue;
            int year = initializer.Year;
            int month = initializer.Month;
            int day = initializer.Day;
            int hour = initializer.Hour;
            int minute = initializer.Minute;
            int second = initializer.Second;
            long ticks = 0;
            string dmtf = dmtfDate;
            System.DateTime datetime = System.DateTime.MinValue;
            string tempString = string.Empty;
            if ((dmtf == null))
            {
                throw new System.ArgumentOutOfRangeException();
            }
            if ((dmtf.Length == 0))
            {
                throw new System.ArgumentOutOfRangeException();
            }
            if ((dmtf.Length != 25))
            {
                throw new System.ArgumentOutOfRangeException();
            }
            try
            {
                tempString = dmtf.Substring(0, 4);
                if (("****" != tempString))
                {
                    year = int.Parse(tempString);
                }
                tempString = dmtf.Substring(4, 2);
                if (("**" != tempString))
                {
                    month = int.Parse(tempString);
                }
                tempString = dmtf.Substring(6, 2);
                if (("**" != tempString))
                {
                    day = int.Parse(tempString);
                }
                tempString = dmtf.Substring(8, 2);
                if (("**" != tempString))
                {
                    hour = int.Parse(tempString);
                }
                tempString = dmtf.Substring(10, 2);
                if (("**" != tempString))
                {
                    minute = int.Parse(tempString);
                }
                tempString = dmtf.Substring(12, 2);
                if (("**" != tempString))
                {
                    second = int.Parse(tempString);
                }
                tempString = dmtf.Substring(15, 6);
                if (("******" != tempString))
                {
                    ticks = (long.Parse(tempString) * ((long)((System.TimeSpan.TicksPerMillisecond / 1000))));
                }
                if ((year < 0)
                    || (month < 0)
                    || (day < 0)
                    || (hour < 0)
                    || (minute < 0)
                    || (second < 0)
                    || (ticks < 0))
                {
                    throw new System.ArgumentOutOfRangeException();
                }
            }
            catch (System.Exception e)
            {
                throw new System.ArgumentOutOfRangeException(null, e.Message);
            }
            datetime = new System.DateTime(year, month, day, hour, minute, second, 0);
            datetime = datetime.AddTicks(ticks);
            System.TimeSpan tickOffset = System.TimeZone.CurrentTimeZone.GetUtcOffset(datetime);
            int UTCOffset = 0;
            int OffsetToBeAdjusted = 0;
            long OffsetMins = ((long)((tickOffset.Ticks / System.TimeSpan.TicksPerMinute)));
            tempString = dmtf.Substring(22, 3);
            if ((tempString != "******"))
            {
                tempString = dmtf.Substring(21, 4);
                try
                {
                    UTCOffset = int.Parse(tempString);
                }
                catch (System.Exception e)
                {
                    throw new System.ArgumentOutOfRangeException(null, e.Message);
                }
                OffsetToBeAdjusted = ((int)((OffsetMins - UTCOffset)));
                datetime = datetime.AddMinutes(((double)(OffsetToBeAdjusted)));
            }
            return datetime;
        }
        private ManagementBaseObject processObj;
        private int processID;
        private int parentProcessID;
        private string commandLine;
        private string executablePath;
        private string name;
        private DateTime creationDate;
        private long cpuTime100ns;
        private static List<ProcessInfo> emptyList = new List<ProcessInfo>();
        #endregion
    }

    /// <summary>
    /// code:ProcessInfos represents the collection of all processes on the machine.   When you create a
    /// ProcessInfos object, a snapshot of process information is taken, which can then be traversed.  
    /// </summary>
    public class ProcessInfos
    {
        /// <summary>
        /// Create a new snapshot of the processes on the machine.  
        /// </summary>
        public ProcessInfos()
        {
            allProcs = new Dictionary<int, ProcessInfo>();
            topProcs = new List<ProcessInfo>();

            ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_Process");
            foreach (ManagementObject processObj in searcher.Get())
            {
                ProcessInfo processInfo = new ProcessInfo(processObj);

                // Process ID 0 is a special Peudo-process that is weird (it is its own parent)
                if (processInfo.ProcessID != 0)
                {
                    allProcs.Add(processInfo.ProcessID, processInfo);
                }
            }

            // create the lists of children based on the parent information 
            foreach (ProcessInfo process in allProcs.Values)
            {
                ProcessInfo parentProcess;
                if (allProcs.TryGetValue(process.ParentProcessID, out parentProcess))
                {
                    // All zero element lists are shared, if we are going to add an element we need to
                    // do copy on write. 
                    if (parentProcess.children.Count == 0)
                    {
                        parentProcess.children = new List<ProcessInfo>();
                    }

                    parentProcess.Children.Add(process);
                    process.parent = parentProcess;
                }
                else
                {
                    topProcs.Add(process);      // It does not have a parent.
                }
            }
        }
        /// <summary>
        /// Used to enumerate all the processes that existed at the time the snapshot was taken
        /// </summary>
        public ICollection<ProcessInfo> Processes { get { return allProcs.Values; } }
        /// <summary>
        /// Look up a particular process by process ID.  If it does not exist, null is returned. 
        /// </summary>
        public ProcessInfo this[int processID]
        {
            get
            {
                ProcessInfo ret = null;
                allProcs.TryGetValue(processID, out ret);
                return ret;
            }
        }
        /// <summary>
        /// Return the collection of all processes that do not have a parent.  This is the natural
        /// starting point for traversing the processes organized by which process spawned which.
        /// </summary>
        public ICollection<ProcessInfo> Orphans { get { return topProcs; } }
        /// <summary>
        /// Returns a 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-------------------------------------------------------------------------------");
            sb.AppendLine(" Proc  Run    CPU    WS Command Line      (children indented after parents)");
            sb.AppendLine("  ID   Time   Time  Meg                       " + DateTime.Now);
            sb.AppendLine("-------------------------------------------------------------------------------");


            foreach (ProcessInfo process in topProcs)
            {
                PrintTree(process, sb, "");
            }

            return sb.ToString();
        }

        #region Private
        private void PrintTree(ProcessInfo process, StringBuilder sb, string indent)
        {
            sb.AppendLine(process.ToString(indent, 54));
            foreach (ProcessInfo child in process.Children)
            {
                PrintTree(child, sb, indent + " ");
            }
        }
        private List<ProcessInfo> topProcs;             // Processes with no parent (that is alive)
        private Dictionary<int, ProcessInfo> allProcs;
        #endregion
    }
}

