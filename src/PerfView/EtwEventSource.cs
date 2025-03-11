using EventSources;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Triggers;
using Utilities;
using EventSource = EventSources.EventSource;

namespace PerfView
{
    /// <summary>
    /// The EventViewer takes a abstract EventSource and displays it.  ETWEventSource
    /// is the implementation of the abstract EventSource class for ETW data.  
    /// </summary>
    public class ETWEventSource : EventSource
    {
        public ETWEventSource(TraceLog traceLog)
        {
            m_tracelog = traceLog;
            NonRestFields = 10;
            MaxEventTimeRelativeMsec = traceLog.SessionDuration.TotalMilliseconds;
            SessionStartTime = traceLog.SessionStartTime;
            OriginTimeZone = TimeZoneInfo.CreateCustomTimeZone("origin", TimeSpan.FromMinutes(traceLog.UTCOffsetMinutes ?? 0), string.Empty, string.Empty);
        }
        public override ICollection<string> EventNames
        {
            get
            {
                if (m_eventNames == null)
                {
                    m_eventNames = new List<string>();
                    m_nameToCounts = new Dictionary<string, TraceEventCounts>();
                    foreach (var counts in m_tracelog.Stats)
                    {
                        var eventName = counts.FullName;
                        if (!m_nameToCounts.ContainsKey(eventName))
                        {
                            m_eventNames.Add(eventName);
                        }

                        m_nameToCounts[eventName] = counts; // we assume if there are collisions the others have the same fields  
                        // so it does not matter which one we pick.  
                    }
                    m_eventNames.Sort();
                }
                return m_eventNames;
            }
        }
        public override void SetEventFilter(List<string> eventNames)
        {
            m_selectedAllEvents = (eventNames.Count >= EventNames.Count);

            m_selectedEvents = new Dictionary<string, bool>();
            foreach (var eventName in eventNames)
            {
                m_selectedEvents[eventName] = false;
                // If this is a stop, mark the corresponding /Start as included (but not to be shown).  
                if (eventName.EndsWith("/Stop") || eventName.EndsWith("/End"))
                {
                    var startName = eventName.Substring(0, eventName.LastIndexOf('/')) + "/Start";
                    if (!m_selectedEvents.ContainsKey(startName))
                    {
                        m_selectedEvents.Add(startName, true);
                    }
                }
            }
        }
        public override void ForEach(Func<EventRecord, bool> callback)
        {
            int cnt = 0;
            double startTime = StartTimeRelativeMSec;
            double endTime = EndTimeRelativeMSec;
            // TODO could be more efficient about process filtering by getting all the processes that match.  
            Regex procRegex = null;
            string procNameMustStartWith = null;
            if (ProcessFilterRegex != null)
            {
                // As an optimization, find the part that is just alphaNumeric
                procNameMustStartWith = Regex.Match(ProcessFilterRegex, @"^(\w*)").Groups[1].Value;
                procRegex = new Regex(ProcessFilterRegex, RegexOptions.IgnoreCase);
            }

            Predicate<ETWEventRecord> textFilter = null;
            if (!string.IsNullOrWhiteSpace(TextFilterRegex))
            {
                string pat = TextFilterRegex;
                bool negate = false;
                if (pat.StartsWith("!"))
                {
                    negate = true;
                    pat = pat.Substring(1);
                }
                var textRegex = new Regex(pat, RegexOptions.IgnoreCase);
                textFilter = delegate (ETWEventRecord eventRecord)
                {
                    bool match = eventRecord.Matches(textRegex);
                    return negate ? !match : match;
                };
            }

            Dictionary<string, int> columnOrder = null;
            ColumnSums = null;
            if (ColumnsToDisplay != null)
            {
                columnOrder = new Dictionary<string, int>();
                for (int i = 0; i < ColumnsToDisplay.Count;)
                {
                    // Discard duplicate columns
                    if (columnOrder.ContainsKey(ColumnsToDisplay[i]))
                    {
                        ColumnsToDisplay.RemoveAt(i);
                        continue;
                    }
                    columnOrder.Add(ColumnsToDisplay[i], i);
                    i++;
                }

                ColumnSums = new double[ColumnsToDisplay.Count];
            }

            if (m_selectedEvents != null)
            {
                ETWEventRecord emptyEventRecord = new ETWEventRecord(this);
                var startStopRecords = new Dictionary<StartStopKey, double>(10);

                // Figure out if you need m_activityComputer or not 
                // Because it is moderately expensive, and not typically used, we only include the activity stuff 
                // when you explicitly ask for it 
                m_needsComputers = false;
                if (ColumnsToDisplay != null)
                {
                    foreach (string column in ColumnsToDisplay)
                    {
                        if (column == "*" || column == "ActivityInfo" || column == "StartStopActivity")
                        {
                            m_needsComputers = true;
                            break;
                        }
                    }
                }

                /***********************************************************************/
                /*                        The main event loop                          */
                EventVisitedVersion.CurrentVersion++;
                var source = m_tracelog.Events.FilterByTime(m_needsComputers ? 0 : startTime, endTime).GetSource(); // If you need computers, you need the events from the start.  
                if (m_needsComputers)
                {
                    m_activityComputer = new ActivityComputer(source, App.GetSymbolReader());
                    m_startStopActivityComputer = new StartStopActivityComputer(source, m_activityComputer);
                }
                source.AllEvents += delegate (TraceEvent data)
                {
                    // FilterByTime would cover this, however for m_needsComputer == true we may not be able to do it that way.  
                    if (data.TimeStampRelativeMSec < startTime)
                    {
                        return;
                    }

                    double durationMSec = -1;
                    var eventFilterVersion = data.EventTypeUserData as EventVisitedVersion;

                    if (eventFilterVersion == null || eventFilterVersion.Version != EventVisitedVersion.CurrentVersion)
                    {
                        var eventName = data.ProviderName + "/" + data.EventName;

                        bool processButDontShow = false;
                        var shouldKeep = m_selectedAllEvents;
                        if (!shouldKeep)
                        {
                            if (m_selectedEvents.TryGetValue(eventName, out processButDontShow))
                            {
                                shouldKeep = true;
                            }
                        }

                        eventFilterVersion = new EventVisitedVersion(shouldKeep, processButDontShow);
                        if (!(data is UnhandledTraceEvent))
                        {
                            data.EventTypeUserData = eventFilterVersion;
                        }
                    }
                    if (!eventFilterVersion.ShouldProcess)
                    {
                        return;
                    }

                    // If this is a StopEvent compute the DURATION_MSEC
                    var opcode = data.Opcode;
                    var task = data.Task;
                    CorelationOptions corelationOptions = CorelationOptions.None;
                    if (data.ProviderGuid == ClrTraceEventParser.ProviderGuid)
                    {
                        // Fix Suspend and restart events to line up to make durations.   
                        if ((int)data.ID == 9)          // SuspendEEStart
                        {
                            corelationOptions = CorelationOptions.UseThreadContext;
                            task = (TraceEventTask)0xFFFE;      // unique task
                            opcode = TraceEventOpcode.Start;
                        }
                        else if ((int)data.ID == 8)     // SuspendEEStop
                        {
                            corelationOptions = CorelationOptions.UseThreadContext;
                            task = (TraceEventTask)0xFFFE;      // unique task  (used for both suspend and Suspend-Restart. 
                            opcode = TraceEventOpcode.Stop;
                        }
                        else if ((int)data.ID == 3)     // RestartEEStop
                        {
                            corelationOptions = CorelationOptions.UseThreadContext;
                            task = (TraceEventTask)0xFFFE;      // unique task
                            opcode = TraceEventOpcode.Stop;
                        }
                    }

                    if (data.ProviderGuid == httpServiceProviderGuid)
                    {
                        corelationOptions = CorelationOptions.UseActivityID;
                        if (opcode == (TraceEventOpcode)13)    // HttpServiceDeliver
                        {
                            opcode = TraceEventOpcode.Start;
                        }
                        // HttpServiceSendComplete  ZeroSend FastSend
                        else if (opcode == (TraceEventOpcode)51 || opcode == (TraceEventOpcode)22 || opcode == (TraceEventOpcode)21)
                        {
                            opcode = TraceEventOpcode.Stop;
                        }
                    }

                    if (data.ProviderGuid == systemDataProviderGuid)
                    {
                        corelationOptions = CorelationOptions.UseActivityID;
                        if ((int)data.ID == 1)          // BeginExecute
                        {
                            task = (TraceEventTask)0xFFFE;      // unique task but used for both BeginExecute and EndExecute. 
                            opcode = TraceEventOpcode.Start;

                        }
                        else if ((int)data.ID == 2)    // EndExecute
                        {
                            task = (TraceEventTask)0xFFFE;      // unique task but used for both BeginExecute and EndExecute. 
                            opcode = TraceEventOpcode.Stop;
                        }
                    }

                    if (opcode == TraceEventOpcode.Start || opcode == TraceEventOpcode.Stop)
                    {
                        // Figure out what we use as a correlater between the start and stop.  
                        Guid contextID = GetCoorelationIDForEvent(data, corelationOptions);
                        var key = new StartStopKey(data.ProviderGuid, task, contextID);
                        if (opcode == TraceEventOpcode.Start)
                        {
                            startStopRecords[key] = data.TimeStampRelativeMSec;
                        }
                        else
                        {
                            double startTimeStamp;
                            if (startStopRecords.TryGetValue(key, out startTimeStamp))
                            {
                                durationMSec = data.TimeStampRelativeMSec - startTimeStamp;

                                // A bit of a hack.  WE use the same start event (SuspenEEStart) for two durations.
                                // Thus don't remove it after SuspendEEStop because we also use it for RestartEEStop.  
                                if (!(task == (TraceEventTask)0xFFFE && (int)data.ID == 8)) // Is this the SuspendEEStop event?
                                {
                                    startStopRecords.Remove(key);
                                }
                            }
                        }
                    }

                    if (!eventFilterVersion.ShouldShow)
                    {
                        return;
                    }

                    if (procRegex != null)
                    {
                        CSwitchTraceData cSwitch = data as CSwitchTraceData;
                        if (!data.ProcessName.StartsWith(procNameMustStartWith, StringComparison.OrdinalIgnoreCase))
                        {
                            if (cSwitch == null)
                            {
                                return;
                            }
                            // Special case.  Context switches will work for both the old and the new process
                            if (!cSwitch.OldProcessName.StartsWith(procNameMustStartWith, StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }
                        }

                        var fullProcessName = data.ProcessName;
                        if (!fullProcessName.StartsWith("("))
                        {
                            fullProcessName += " (" + data.ProcessID + ")";
                        }

                        if (!procRegex.IsMatch(fullProcessName))
                        {
                            if (cSwitch == null)
                            {
                                return;
                            }
                            // Special case.  Context switches will work for both the old and the new process
                            var fullOldProcessName = cSwitch.OldProcessName;
                            if (!fullOldProcessName.StartsWith("("))
                            {
                                fullOldProcessName += " (" + cSwitch.OldProcessName + ")";
                            }

                            if (!procRegex.IsMatch(fullOldProcessName))
                            {
                                return;
                            }
                        }
                    }
                    
                    ETWEventRecord eventRecord = null;
                    if (textFilter != null)
                    {
                        eventRecord = new ETWEventRecord(this, data, columnOrder, NonRestFields, durationMSec);
                        if (!textFilter(eventRecord))
                        {
                            return;
                        }
                    }

                    if (FilterQueryExpressionTree != null)
                    {
                        var match = FilterQueryExpressionTree.Match(data);
                        if (!match)
                        {
                            return;
                        }
                    }

                    cnt++;
                    if (MaxRet < cnt)
                    {
                        // We have exceeded our MaxRet, return an empty record.  
                        eventRecord = emptyEventRecord;
                        eventRecord.m_timeStampRelativeMSec = data.TimeStampRelativeMSec;
                    }
                    
                    
                    if (eventRecord == null)
                    {
                        eventRecord = new ETWEventRecord(this, data, columnOrder, NonRestFields, durationMSec);
                    }

                    if (ColumnSums != null)
                    {
                        var fields = eventRecord.DisplayFields;
                        var min = Math.Min(ColumnSums.Length, fields.Length);
                        for (int i = 0; i < min; i++)
                        {
                            string value = fields[i];
                            double asDouble;
                            if (value != null && double.TryParse(value, out asDouble))
                            {
                                ColumnSums[i] += asDouble;
                            }
                        }
                    }
                    if (!callback(eventRecord))
                    {
                        source.StopProcessing();
                    }
                };
                source.Process();
            }
        }

        public DateTime SessionStartTime { get; private set; }
        public TimeZoneInfo OriginTimeZone { get; private set; }

        [Flags]
        private enum CorelationOptions
        {
            None = 0,
            UseThreadContext = 1,
            UseActivityID = 2,
        }

        private static readonly Guid httpServiceProviderGuid = new Guid("dd5ef90a-6398-47a4-ad34-4dcecdef795f");
        private static readonly Guid systemDataProviderGuid = new Guid("6a4dfe53-eb50-5332-8473-7b7e10a94fd1");

        private unsafe Guid GetCoorelationIDForEvent(TraceEvent data, CorelationOptions options)
        {
            int? intContextID = null;      // When the obvious ID is an integer 
            if ((options & CorelationOptions.UseThreadContext) == 0)
            {
                if ((options & CorelationOptions.UseActivityID) == 0)
                {
                    // If the payloads have parameters that indicate it is a correlation event, use that.  
                    var names = data.PayloadNames;
                    if (names != null && names.Length > 0)
                    {
                        int fieldNum = -1;    // First try to use a field as the correlater
                        if (0 < names.Length)
                        {
                            if (names[0].EndsWith("id", StringComparison.OrdinalIgnoreCase) ||
                                string.Compare("Name", names[0], StringComparison.OrdinalIgnoreCase) == 0 ||    // Used for simple generic taskss
                                names[0] == "Count") // Hack for GC/Start 
                            {
                                fieldNum = 0;
                            }
                            else if (1 < names.Length && names[1] == "ContextId")       // This is for ASP.NET events 
                            {
                                fieldNum = 1;
                            }
                        }

                        if (0 <= fieldNum)
                        {
                            var value = data.PayloadValue(fieldNum);
                            if (value is Guid)
                            {
                                return (Guid)value;
                            }
                            else
                            {
                                if (value != null)
                                {
                                    intContextID = value.GetHashCode();                 // Use the hash if it is not a GUID
                                }
                            }
                        }
                    }
                }
                // If we have not found a context field, and there is an activity ID use that.  
                if (data.ActivityID != Guid.Empty)
                {
                    if (!intContextID.HasValue)
                    {
                        return data.ActivityID;
                    }

                    //TODO Currently, people may have recursive tasks that are not marked (because they can't if they want it to work before V4.6)
                    // For now we don't try to correlate with activity IDS.  
                    if (false && StartStopActivityComputer.IsActivityPath(data.ActivityID, data.ProcessID))
                    {
                        int guidHash = data.ActivityID.GetHashCode();
                        // Make up a correlater that is the combination of both the value and the Activity ID, the tail is arbitrary 
                        // TODO this is causing unnecessary collisions. 
                        return new Guid(intContextID.Value, (short)guidHash, (short)(guidHash >> 16), 45, 34, 34, 67, 4, 4, 5, 5);
                    }
                }
            }

            // If we have not found a context, use the thread as a context.  
            if (!intContextID.HasValue)
            {
                intContextID = data.ThreadID;                  // By default use the thread as the correlation ID
            }

            return new Guid(intContextID.Value, 1, 5, 45, 23, 23, 3, 5, 5, 4, 5);
        }

        public override ICollection<string> ProcessNames
        {
            get
            {
                var set = new SortedDictionary<string, string>();
                foreach (var process in m_tracelog.Processes)
                {
                    if (process.ProcessID > 0 &&
                        process.Name != "svchost" && process.Name != "winlogon" && process.Name != "conhost")
                    {
                        set[process.Name] = "";
                    }
                }
                return set.Keys;
            }
        }

        public override ICollection<string> AllColumnNames(List<string> eventNames)
        {
            var columnsForSelectedEvents = new SortedDictionary<string, string>();
            var selectedEventCounts = GetEventCounts(eventNames);
            foreach (var selectedEventCount in selectedEventCounts.Keys)
            {
                var payloadNames = selectedEventCount.PayloadNames;
                if (payloadNames != null)
                {
                    foreach (var fieldName in payloadNames)
                    {
                        columnsForSelectedEvents[fieldName] = fieldName;
                    }
                }
            }
            columnsForSelectedEvents["ActivityInfo"] = "ActivityInfo";
            columnsForSelectedEvents["StartStopActivity"] = "StartStopActivity";
            columnsForSelectedEvents["ThreadID"] = "ThreadID";
            columnsForSelectedEvents["ProcessorNumber"] = "ProcessorNumber";
            columnsForSelectedEvents["ActivityID"] = "ActivityID";
            columnsForSelectedEvents["RelatedActivityID"] = "RelatedActivityID";
            columnsForSelectedEvents["HasStack"] = "HasStack";
            columnsForSelectedEvents["HasBlockingStack"] = "HasBlockingStack";
            columnsForSelectedEvents["DURATION_MSEC"] = "DURATION_MSEC";
            columnsForSelectedEvents["FormattedMessage"] = "FormattedMessage";
            columnsForSelectedEvents["ContainerID"] = "ContainerID";
            return columnsForSelectedEvents.Keys;
        }
        public override EventSource Clone()
        {
            return new ETWEventSource(m_tracelog);
        }

        public TraceLog Log { get { return m_tracelog; } }

        #region private
        private Dictionary<TraceEventCounts, TraceEventCounts> GetEventCounts(List<string> eventNames)
        {
            var selectedEventCounts = new Dictionary<TraceEventCounts, TraceEventCounts>();
            foreach (var eventName in eventNames)
            {
                var eventCounts = m_nameToCounts[eventName];
                selectedEventCounts[eventCounts] = eventCounts;
            }
            return selectedEventCounts;
        }

        private TraceLog m_tracelog;
        private bool m_needsComputers;          // True if you are looking at fields that need m_activityComputer or m_startStopActivityComputer
        private ActivityComputer m_activityComputer;
        private StartStopActivityComputer m_startStopActivityComputer;
        private Dictionary<string, TraceEventCounts> m_nameToCounts;
        private List<string> m_eventNames;
        private Dictionary<string, bool> m_selectedEvents;      // set to true if the event is only present because it is a start for a stop.  
        private bool m_selectedAllEvents;       // This ensures that when a user selects all events he gets everything 

        
        internal class ETWEventRecord : EventRecord
        {
            // Used as the null record (after MaxRet happens). 
            internal ETWEventRecord(ETWEventSource source) : base(0) { m_source = source; }

            internal ETWEventRecord(ETWEventSource source, TraceEvent data, Dictionary<string, int> columnOrder, int nonRestFields, double durationMSec)
                : base(nonRestFields)
            {
                m_source = source;
                m_name = data.ProviderName + "/" + data.EventName;
                m_processName = data.ProcessName;
                if (!m_processName.StartsWith("("))
                {
                    m_processName += " (" + data.ProcessID + ")";
                }

                m_timeStampRelativeMSec = data.TimeStampRelativeMSec;
                m_idx = data.EventIndex;
                m_payloads = new List<Payload>();
                
                // Compute the data column 
                var restString = new StringBuilder();

                // Deal with the special HasStack, ThreadID and ActivityID, DataLength fields;
                var hasStack = data.CallStackIndex() != CallStackIndex.Invalid;
                if (hasStack)
                {
                    AddField("HasStack", hasStack.ToString(), columnOrder, restString, m_payloads);
                }

                var asCSwitch = data as CSwitchTraceData;
                if (asCSwitch != null)
                {
                    AddField("HasBlockingStack", (asCSwitch.BlockingStack() != CallStackIndex.Invalid).ToString(), columnOrder, restString, m_payloads);
                }

                AddField("ThreadID", data.ThreadID.ToString("n0"), columnOrder, restString, m_payloads);
                AddField("ProcessorNumber", data.ProcessorNumber.ToString(), columnOrder, restString, m_payloads);

                if (0 < durationMSec)
                {
                    AddField("DURATION_MSEC", durationMSec.ToString("n3"), columnOrder, restString, m_payloads);
                }

                var payloadNames = data.PayloadNames;
                if (payloadNames.Length == 0 && data.EventDataLength != 0)
                {
                    // WPP events look classic and use the EventID as their discriminator
                    if (data.IsClassicProvider && data.ID != 0)
                    {
                        AddField("EventID", ((int)data.ID).ToString(), columnOrder, restString, m_payloads);
                    }

                    AddField("DataLength", data.EventDataLength.ToString(), columnOrder, restString, m_payloads);
                }

                try
                {
                    for (int i = 0; i < payloadNames.Length; i++)
                    {
                        AddField(payloadNames[i], data.PayloadString(i), columnOrder, restString, m_payloads);
                    }
                }
                catch (Exception e)
                {
                    AddField("ErrorParsingFields", e.Message, columnOrder, restString, m_payloads);
                }

                var message = data.FormattedMessage;
                if (message != null)
                {
                    AddField("FormattedMessage", message, columnOrder, restString, m_payloads);
                }

                if (source.m_needsComputers)
                {
                    TraceThread thread = data.Thread();
                    if (thread != null)
                    {
                        TraceActivity activity = source.m_activityComputer.GetCurrentActivity(thread);
                        if (activity != null)
                        {
                            string id = activity.ID;
                            if (Math.Abs(activity.StartTimeRelativeMSec - m_timeStampRelativeMSec) < .0005)
                            {
                                id = "^" + id;              // Indicates it is at the start of the task. 
                            }

                            AddField("ActivityInfo", id, columnOrder, restString, m_payloads    );
                        }

                        var startStopActivity = source.m_startStopActivityComputer.GetCurrentStartStopActivity(thread, data);
                        if (startStopActivity != null)
                        {
                            string name = startStopActivity.Name;
                            string parentName = "$";
                            if (startStopActivity.Creator != null)
                            {
                                parentName = startStopActivity.Creator.Name;
                            }

                            AddField("StartStopActivity", name + "/P=" + parentName, columnOrder, restString, m_payloads);
                        }
                    }
                }

                // We pass 0 as the process ID for creating the activityID because we want uniform syntax.  
                if (data.ActivityID != Guid.Empty)
                {
                    AddField("ActivityID", StartStopActivityComputer.ActivityPathString(data.ActivityID), columnOrder, restString, m_payloads);
                }

                Guid relatedActivityID = data.RelatedActivityID;
                if (relatedActivityID != Guid.Empty)
                {
                    AddField("RelatedActivityID", StartStopActivityComputer.ActivityPathString(data.RelatedActivityID), columnOrder, restString, m_payloads);
                }

                if(data.ContainerID != null)
                {
                    AddField("ContainerID", data.ContainerID, columnOrder, restString, m_payloads);
                }

                m_asText = restString.ToString();
            }

            public override string EventName { get { return m_name; } }
            public override string ProcessName { get { return m_processName; } }
            public override double TimeStampRelatveMSec { get { return m_timeStampRelativeMSec; } }
            public DateTime LocalTimeStamp { get { return this.m_source.SessionStartTime.AddMilliseconds(this.m_timeStampRelativeMSec); } }
            public DateTime OriginTimeStamp { get { return TimeZoneInfo.ConvertTime(LocalTimeStamp, this.m_source.OriginTimeZone); } }
            public override string Rest { get { return m_asText; } set { } }
            public EventIndex Index { get { return m_idx; } }
            public override List<Payload> Payloads { get { return m_payloads; }  }
            
            #region private

            private static readonly Regex specialCharRemover = new Regex(" *[\r\n\t]+ *", RegexOptions.Compiled);

            /// <summary>
            /// Adds 'fieldName' with value 'fieldValue' to the output.  It either goes into a column (based on columnOrder) or it goes into
            /// 'rest' as a fieldName="fieldValue" string.   It also updates 'columnSums' for the fieldValue for any in a true column 
            /// </summary>
            private void AddField(string fieldName, string fieldValue, Dictionary<string, int> columnOrder, StringBuilder restString, List<Payload> payloadsList)
            {
                if (fieldValue == null)
                {
                    fieldValue = "";
                }

                payloadsList.Add(new Payload(fieldName, fieldValue));

                // If the field value has to many newlines in it, the GUI gets confused because the text block is larger than
                // the vertical size.   WPF may fix this at some point, but in the mean time this is a work around. 
                fieldValue = specialCharRemover.Replace(fieldValue, " ");

                var putInRest = true;
                if (columnOrder != null)
                {
                    int colNum;
                    putInRest = false;
                    if (columnOrder.TryGetValue(fieldName, out colNum))
                    {
                        putInRest = false;
                        if (colNum < m_displayFields.Length)
                        {
                            m_displayFields[colNum] = PadIfNumeric(fieldValue);
                        }
                        else
                        {
                            putInRest = true;
                        }
                    }
                }
                if (putInRest)
                {
                    restString.Append(fieldName).Append("=").Append(Command.Quote(fieldValue)).Append(' ');
                }
            }

            /// <summary>
            /// Hack to make sort work properly most of the time.   Basically if 'fieldValue' looks like a number pad it with 
            /// spaces to the left to make sure it sorts like a number.   This works for numbers with 6 digits (or comma) in
            /// front of the decimal point.   Thus it works up to 99,999.999
            /// </summary>  
            private string PadIfNumeric(string fieldValue)
            {
                // Hack, if it looks numeric, pad so it sorts like one.  It is a hack because we don't know how much to pad, we guess 6 after the dot.  
                int charsAfterDot = 0;
                bool seenDot = false;
                char decimalPoint = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
                char separatorChar = '\0';
                string separator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
                if (0 < separator.Length)
                {
                    separatorChar = separator[0];
                }

                for (int idx = 0; idx < fieldValue.Length; idx++)
                {
                    char c = fieldValue[idx];
                    if (c == decimalPoint)
                    {
                        if (seenDot)
                        {
                            return fieldValue;      // Not numeric.
                        }

                        seenDot = true;
                        charsAfterDot = fieldValue.Length - idx;
                    }
                    else if (!Char.IsDigit(c) && c != separatorChar)
                    {
                        return fieldValue;          // Not numeric.
                    }
                }
                return fieldValue.PadLeft(6 + charsAfterDot);
            }

            public override bool Matches(Regex textRegex)
            {
                if (textRegex.IsMatch(Rest))
                {
                    return true;
                }

                for (int i = 0; i < m_displayFields.Length; i++)
                {
                    var field = m_displayFields[i];
                    if (field != null && textRegex.IsMatch(field))
                    {
                        return true;
                    }
                }
                if (textRegex.IsMatch(EventName))
                {
                    return true;
                }

                if (textRegex.IsMatch(ProcessName))
                {
                    return true;
                }

                if (textRegex.IsMatch(TimeStampRelatveMSec.ToString("n3")))
                {
                    return true;
                }

                return false;
            }

            private string m_name;
            private string m_processName;
            internal double m_timeStampRelativeMSec;
            private string m_asText;
            private EventIndex m_idx;
            private ETWEventSource m_source;        // Lets you get at source information
            private List<Payload> m_payloads;
            #endregion
        }

        // We tag every event template as we see it with whether we should filter it or not
        // However we need to have a version number associated with it so that we don't use 'old' 
        // filters.  That is what EventVisitedVersion does.  
        private class EventVisitedVersion
        {
            public static int CurrentVersion;
            public EventVisitedVersion(bool shouldKeep, bool processButDontShow)
            {
                Version = CurrentVersion;
                ShouldShow = shouldKeep && !processButDontShow;
                ShouldProcess = shouldKeep || processButDontShow;
            }
            public readonly int Version;
            public readonly bool ShouldShow;
            /// <summary>
            /// We match start and stop opcodes.  We want to allow Start opcodes even if they are not selected to ensure that
            /// we can compute the duration between start and stop events.  
            /// </summary>
            public readonly bool ShouldProcess;
        }
        #endregion
    }
}
