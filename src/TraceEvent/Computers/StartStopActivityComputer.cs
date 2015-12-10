// Copyright (c) Microsoft Corporation.  All rights reserved
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.AspNet;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Tracing
{
    /// <summary>
    /// Calculates start-stop activities (computes duration), if they use activity paths, we can compute 
    /// causality and hook that up as well.  
    /// </summary>
    public class StartStopActivityComputer
    {
        /// <summary>
        /// Create a new ServerRequest Computer.
        /// </summary>
        public StartStopActivityComputer(TraceLogEventSource source, ActivityComputer taskComputer)
        {
            taskComputer.NoCache = true;            // Can't cache start-stops (at the moment)
            m_source = source;
            m_activeActivities = new Dictionary<StartStopKey, StartStopActivity>();
            m_activeActivitiesByActivityId = new Dictionary<Guid, StartStopActivity>();
            m_taskComputer = taskComputer;

            // Whenever a new Activity is created, propagate the start-stop activity from the creator
            // to the created task.  
            taskComputer.Create += delegate(TraceActivity activity, TraceEvent data)
            {
                TraceActivity creator = activity.Creator;
                if (creator == null)
                    return;

                StartStopActivity startStopActivity = m_traceActivityToStartStopActivity.Get((int)creator.Index);
                if (startStopActivity == null)
                    return;
                m_traceActivityToStartStopActivity.Set((int)activity.Index, startStopActivity);
            };

            var dynamicParser = source.Dynamic;
            var threadToLastAspNetGuid = new Guid[m_source.TraceLog.Threads.Count];     // Only need to fix up ASPNet activity ids.  
            dynamicParser.All += delegate(TraceEvent data)
            {
                // TODO decide what the correct heuristic is.  
                // Currently I only do this for things that might be an EventSoruce 
                if (!TraceEventProviders.MaybeAnEventSource(data.ProviderGuid))
                    return;

                TraceEventOpcode opcode = data.Opcode;
                TraceEventTask task = data.Task;
                StartStopActivity creator = null;
                StartStopKey extraKey = null;


                // We process some events specially because the are important and don't follow the normal ActivityPath conventions.  
                if (opcode == TraceEventOpcode.Info && data.ProviderGuid == AdoNetProvider)
                {
                    if (data.ID == (TraceEventID)1)        // BeginExecute
                        opcode = TraceEventOpcode.Start;
                    else if (data.ID == (TraceEventID)2)   // EndExecute
                        opcode = TraceEventOpcode.Stop;
                }
                // In V4.6 the activity ID for Microsoft-Windows-ASPNET/Request/Start is improperly set, but we can fix it by 
                // looking at the 'send' event that happens just before it.  
                else if (data.Opcode == (TraceEventOpcode)9 && data.ProviderGuid == MicrosoftWindowsASPNetProvider)
                {
                    TraceThread thread = data.Thread();
                    if (thread != null)
                        threadToLastAspNetGuid[(int)thread.ThreadIndex] = data.RelatedActivityID;
                    return;
                }

                if (opcode == TraceEventOpcode.Start || opcode == TraceEventOpcode.Stop)
                {
                    TraceThread thread = data.Thread();
                    if (thread == null)
                        return;

                    string extraInfo = null;
                    Guid activityID = data.ActivityID;
                    bool goodActivityID = StartStopActivityComputer.IsActivityPath(activityID);

                    string taskName = null;

                    // handle special event we know about 
                    if (data.ProviderGuid == FrameworkEventSourceTraceEventParser.ProviderGuid)
                    {
                        if (data.Task == (TraceEventTask)1 || data.Task == (TraceEventTask)2) // GetResponse or GetResponseStream
                        {
                            Debug.Assert(data.payloadNames[0] == "id");
                            long id = (long)data.PayloadValue(0);
                            // TODO as of 2/2015 there was a bug where the ActivityPath logic was getting this ID wrong, so we get to from the thread. 
                            // relatedActivityID = activityID;
                            creator = GetCurrentStartStopActivity(thread, data);
                            activityID = new Guid((int)id, (short)(id >> 32), (short)(id >> 48), 6, 7, 8, 9, 10, 11, 12, 13);   // Tail 6,7,8,9,10,11,12,13 means GetResponse
                            goodActivityID = true;
                            taskName = "Http" + data.TaskName;

                            if (opcode == TraceEventOpcode.Start)
                            {
                                Debug.Assert(data.payloadNames[1] == "uri");
                                extraInfo = data.PayloadValue(1) as string;
                            }
                        }
                    }
                    else if (data.ProviderGuid == AdoNetProvider)
                    {
                        if (data.ID == (TraceEventID)1 || data.ID == (TraceEventID)2) // BeginExecute or EndExecute
                        {
                            Debug.Assert(data.payloadNames[0] == "objectId");
                            // TODO as of 2/2015 there was a bug where the ActivityPath logic was getting this ID wrong, so we get to from the thread. 
                            // relatedActivityID = activityID;
                            creator = GetCurrentStartStopActivity(thread, data);
                            activityID = new Guid((int)data.PayloadValue(0), 1234, 5, 7, 7, 8, 9, 10, 11, 12, 13);  // Tail 7,7,8,9,10,11,12,13 means SQL 
                            goodActivityID = true;
                            taskName = "SQLCommand";
                            task = (TraceEventTask)1;       // Make up a task for this. 

                            if (opcode == TraceEventOpcode.Start)
                            {
                                Debug.Assert(data.payloadNames[1] == "dataSource");
                                extraInfo = data.PayloadValue(1) as string;
                            }
                        }
                    }
                    else if (data.ProviderGuid == MicrosoftWindowsASPNetProvider)
                    {
                        // MicrosoftWindowsASPNetProvider does not use ActivityPaths but is OK, opt it in.  
                        if (opcode == TraceEventOpcode.Start)
                        {
                            extraInfo = (string)data.PayloadValue(1);           // This is the FullUrl field.  
                            if (goodActivityID)         // V4.6 incorrectly opts this into activity paths (which don't work for stop).  Fix it to not do this. 
                            {
                                // Also be able to lookup by the incorrect key was well.   (Sometimes stop uses one sometimes the other). 
                                Guid activityFix = threadToLastAspNetGuid[(int)thread.ThreadIndex];
                                if (activityFix != Guid.Empty)
                                {
                                    if (activityID != activityFix)  // Only have to produce the extra key when they mismatch (thus on V4.7 we don't).  
                                    {
                                        activityID = activityFix;
                                        extraKey = new StartStopKey(data.ProviderGuid, task, data.ProcessID, activityID);
                                    }
                                }
                                else
                                    Trace.WriteLine(data.TimeStampRelativeMSec.ToString("n3") + " Could not find ASP.NET Send event to fix Start event");
                            }
                        }
                        threadToLastAspNetGuid[(int)thread.ThreadIndex] = Guid.Empty;
                        goodActivityID = true;
                    }
                    else
                    {
                        // Include the first argument if it is a string
                        if (0 < data.PayloadNames.Length)
                        {
                            try
                            {
                                extraInfo = data.PayloadValue(0) as string;
                            }
                            catch (Exception) { }
                            if (extraInfo != null)
                                extraInfo = data.payloadNames[0] + "=" + extraInfo;
                        }
                    }

                    if (goodActivityID)      // Means that activityID is set to something useful. 
                    {
                        StartStopKey key = new StartStopKey(data.ProviderGuid, task, data.ProcessID, activityID);
                        StartStopActivity activity = null;

                        TraceActivity curTaskActivity = m_taskComputer.GetCurrentActivity(thread);
                        int taskIndex = (int)curTaskActivity.Index;

                        // Because we want the stop to logically be 'after' the actual stop event (so that the stop looks like
                        // it is part of the start-stop activity we defer it until the next event.   If there is already
                        // a deferral, we can certainly do that one now.  
                        DoStopIfNecessary(false);

                        if (opcode == TraceEventOpcode.Start)
                        {
                            if (creator == null)
                            {
                                if (data.RelatedActivityID != Guid.Empty)
                                {
                                m_activeActivitiesByActivityId.TryGetValue(data.RelatedActivityID, out creator);
                                if (creator == null)
                                    Trace.WriteLine(data.TimeStampRelativeMSec.ToString("n3") + " Warning: Could not find creator Activity " + StartStopActivityComputer.ActivityPathString(data.RelatedActivityID));
                                }
                            }

                            if (taskName == null)
                                taskName = data.TaskName;

                            // Create the new activity.  
                            activity = new StartStopActivity(data, taskName, ref activityID, creator, extraInfo);
                            m_activeActivities[key] = activity;                         // So we can correlate stops with this start.  
                            if (extraKey != null)
                                m_activeActivities[extraKey] = activity;                // TODO we leak these.   

                            // Remember that this task is doing this activity.          // So we can transfer this activity to any subsequent events on this Task/Thread.  
                            m_activeActivitiesByActivityId[activityID] = activity;      // So we can look it up (for children's related Activity IDs)
                            m_traceActivityToStartStopActivity.Set(taskIndex, activity);

                            // TODO FIX NOW remove.  
                            // Trace.WriteLine(string.Format("\r\n{0,10:n3}: Thread {1} NumActive {2} Task {3}\r\n    START {4}",
                            //     data.TimeStampRelativeMSec, thread.ThreadID, m_activeActivities.Count, curTaskActivity, activity.ActivityPathString));

                            // Issue callback if requested AFTER state update
                            var onStartStop = OnStartOrStop;
                            if (onStartStop != null)
                                onStartStop(activity, false);
                        }
                        else
                        {
                            Debug.Assert(opcode == TraceEventOpcode.Stop);
                            // Find the corresponding start event.  
                            if (m_activeActivities.TryGetValue(key, out activity))
                            {
                                // We defer the stop until the NEXT event (so that stops look like they are IN the activity). 
                                // So here we just capture what is needed to do this later (basically on the next event).  
                                activity.RememberStop(data.EventIndex, data.TimeStampRelativeMSec, key, taskIndex);
                                m_deferredStop = activity;      // Remember this one for deferral.  

                                //Trace.WriteLine(string.Format("{0,10:n3}: Thread {1} NumActive {2} Task {3}\r\n    DEFERRING-STOP {4}",
                                //    data.TimeStampRelativeMSec, thread.ThreadID, m_activeActivities.Count, curTaskActivity, activity.ActivityPathString));

                                // Issue callback if requested AFTER state update
                                var onStartStop = OnStartOrStop;
                                if (onStartStop != null)
                                    onStartStop(activity, true);
                            }
                            else
                            {
                                // TODO GetResponseStream Stops can sometime occur before the start (if they can be accomplished without I/O).  
                                if (!(data.ProviderGuid == FrameworkEventSourceTraceEventParser.ProviderGuid))
                                    Trace.WriteLine("Warning: Unmatched stop at " + data.TimeStampRelativeMSec.ToString("n3") + " key = " + key);
                            }
                        }
                    }
                    else
                        Trace.WriteLine("Skipping start at  " + data.TimeStampRelativeMSec.ToString("n3") + " name = " + data.EventName);
                }
            };
        }
        /// <summary>
        /// The current start-stop activity on the given thread.   
        /// If present 'context' is used to look up the current activityID and try to use that to repair missing Starts.  
        /// Basically if we can't figure out what StartStop activity the thread from just the threadID we can use 
        /// the activityID to find it.  
        /// </summary>
        public StartStopActivity GetCurrentStartStopActivity(TraceThread thread, TraceEvent context=null)
        {
            DoStopIfNecessary(false);        // Do any deferred stops. 

            // Search up the stack of tasks, seeing if we have a start-stop activity. 
            TraceActivity curTaskActivity = m_taskComputer.GetCurrentActivity(thread);
            if (curTaskActivity == null)
                return null;

            int taskIndex = (int)curTaskActivity.Index;
            var ret = m_traceActivityToStartStopActivity.Get(taskIndex);

            if (ret == null && context != null)
            {
                Guid activityID = context.ActivityID;
                if (activityID != Guid.Empty)
                    m_activeActivitiesByActivityId.TryGetValue(activityID, out ret);
            }

            // If the activity is stopped, then don't return it, return its parent.   
            while (ret != null)
            {
                if (!ret.IsStopped)
                    return ret;
                ret = ret.Creator;
            }
            return ret;
        }

        /// <summary>
        /// Returns a stack index representing the nesting of Start-Stop activities for the thread 'curThread' at the current time
        /// (At this point of the current event for the computer).   The stack starts with a frame for the process of the thread, then 
        /// has all the start-stop activity frames, then a frame representing 'topThread' which may not be the same as 'thread' since
        /// 'topThread' is the thread that spawned the first task, not the currently executing thread.  
        /// 
        /// Normally this stack is for the current time, but if 'getAtCreationTime' is true, it will compute the
        /// stack at the time that the current activity was CREATED rather than the current time.  This works 
        /// better for await time
        /// </summary>
        public StackSourceCallStackIndex GetCurrentStartStopActivityStack(MutableTraceEventStackSource outputStackSource, TraceThread curThread, TraceThread topThread, bool getAtCreationTime = false)
        {
            StartStopActivity startStop = null;
            // Best effort to get the activity as it exists at creation time of the current activity (AWAIT).    
            if (getAtCreationTime)
            {
                TraceActivity curTaskActivity = m_taskComputer.GetCurrentActivity(curThread);
                if (curTaskActivity != null)
                {
                    startStop = m_traceActivityToStartStopActivity.Get((int)curTaskActivity.Index);
                    if (startStop != null)
                    {
                        // Check to make sure that the start-stop stopped AFTER the activity was created.   
                        if (startStop.IsStopped && startStop.StartTimeRelativeMSec + startStop.DurationMSec < curTaskActivity.CreationTimeRelativeMSec)
                            startStop = null;
                    }
                }
            }
            if (startStop == null)
                startStop = GetCurrentStartStopActivity(curThread); 

            // Get the process, and activity frames. 
            StackSourceCallStackIndex stackIdx = GetStartStopActivityStack(outputStackSource, startStop, topThread.Process);

            // Add the thread.  
            stackIdx = outputStackSource.Interner.CallStackIntern(outputStackSource.Interner.FrameIntern(topThread.VerboseThreadName), stackIdx);
            return stackIdx;
        }

        /// <summary>
        /// Gets a stack that represents the nesting of the Start-Stop tasks.  curActivity can be null, in which case just he process node is returned.  
        /// </summary>
        public StackSourceCallStackIndex GetStartStopActivityStack(MutableTraceEventStackSource outputStackSource, StartStopActivity curActivity, TraceProcess process)
        {
            StackSourceCallStackIndex stackIdx = outputStackSource.GetCallStackForProcess(process);

            // Add Pseudo frame for whether it is an activity or not 
            string firstFrameName = curActivity == null ? "(Non-Activities)" : "(Activities)";
            stackIdx = outputStackSource.Interner.CallStackIntern(outputStackSource.Interner.FrameIntern(firstFrameName), stackIdx);

            if (curActivity != null)
                stackIdx = curActivity.GetActivityStack(outputStackSource, stackIdx);
            return stackIdx;
        }

        /// <summary>
        /// If non-null is called when a start or stop happens.   It is called AFTER state update for both starts and stops.  
        /// Note that the 'stop' is actually called twice,   First with a 'true' passed to the Action as the second argument 
        /// (think of it as 'before state update') which indicate that we are BEFORE the stop has been processed and later 
        /// with 'false' that indicates the state update has happened.   Starts are only called AFTER (and thus with 'false')
        /// </summary>
        public Action<StartStopActivity, bool> OnStartOrStop;

        /// <summary>
        /// Returns true if 'guid' follow the EventSouce style activity IDs. 
        /// </summary>
        public static unsafe bool IsActivityPath(Guid guid)
        {
            uint* uintPtr = (uint*)&guid;
            return (uintPtr[0] + uintPtr[1] + uintPtr[2] + 0x599D99AD == uintPtr[3]);
        }

        /// <summary>
        /// returns a string representation for the activity path.  If the GUID is not an activity path then it returns
        /// the normal string representation for a GUID. 
        /// </summary>
        public static unsafe string ActivityPathString(Guid guid)
        {
            if (!IsActivityPath(guid))
                return guid.ToString();

            StringBuilder sb = new StringBuilder();
            sb.Append('/'); // Use // to start to make it easy to anchor 
            byte* bytePtr = (byte*)&guid;
            byte* endPtr = bytePtr + 12;
            char separator = '/';
            while (bytePtr < endPtr)
            {
                uint nibble = (uint)(*bytePtr >> 4);
                bool secondNibble = false;              // are we reading the second nibble (low order bits) of the byte.
            NextNibble:
                if (nibble == (uint)NumberListCodes.End)
                    break;
                if (nibble <= (uint)NumberListCodes.LastImmediateValue)
                {
                    sb.Append('/').Append(nibble);
                    if (!secondNibble)
                    {
                        nibble = (uint)(*bytePtr & 0xF);
                        secondNibble = true;
                        goto NextNibble;
                    }
                    // We read the second nibble so we move on to the next byte. 
                    bytePtr++;
                    continue;
                }
                else if (nibble == (uint)NumberListCodes.PrefixCode)
                {
                    // This are the prefix codes.   If the next nibble is MultiByte, then this is an overflow ID.  
                    // we we denote with a $ instead of a / separator.  

                    // Read the next nibble.  
                    if (!secondNibble)
                        nibble = (uint)(*bytePtr & 0xF);
                    else
                    {
                        bytePtr++;
                        if (endPtr <= bytePtr)
                            break;
                        nibble = (uint)(*bytePtr >> 4);
                    }

                    if (nibble < (uint)NumberListCodes.MultiByte1)
                    {
                        // If the nibble is less than MultiByte we have not defined what that means 
                        // For now we simply give up, and stop parsing.  We could add more cases here...
                        return guid.ToString();
                    }
                    // If we get here we have a overflow ID, which is just like a normal ID but the separator is $
                    separator = '$';
                    // Fall into the Multi-byte decode case.  
                }

                Debug.Assert((uint)NumberListCodes.MultiByte1 <= nibble);
                // At this point we are decoding a multi-byte number, either a normal number or a 
                // At this point we are byte oriented, we are fetching the number as a stream of bytes. 
                uint numBytes = nibble - (uint)NumberListCodes.MultiByte1;

                uint value = 0;
                if (!secondNibble)
                    value = (uint)(*bytePtr & 0xF);
                bytePtr++;       // Adance to the value bytes

                numBytes++;     // Now numBytes is 1-4 and reprsents the number of bytes to read.  
                if (endPtr < bytePtr + numBytes)
                    break;

                // Compute the number (little endian) (thus backwards).  
                for (int i = (int)numBytes - 1; 0 <= i; --i)
                    value = (value << 8) + bytePtr[i];

                // Print the value
                sb.Append(separator).Append(value);

                bytePtr += numBytes;        // Advance past the bytes.
            }

            sb.Append('/');
            return sb.ToString();
        }

        #region private
        static readonly Guid MicrosoftWindowsASPNetProvider = new Guid("ee799f41-cfa5-550b-bf2c-344747c1c668");

        /// <summary>
        /// The encoding for a list of numbers used to make Activity  Guids.   Basically
        /// we operate on nibbles (which are nice because they show up as hex digits).  The
        /// list is ended with a end nibble (0) and depending on the nibble value (Below)
        /// the value is either encoded into nibble itself or it can spill over into the
        /// bytes that follow.   
        /// </summary>
        private enum NumberListCodes : byte
        {
            End = 0x0,             // ends the list.   No valid value has this prefix.   
            LastImmediateValue = 0xA,
            PrefixCode = 0xB,

            MultiByte1 = 0xC,   // 1 byte follows.  If this Nibble is in the high bits, it the high bits of the number are stored in the low nibble.   
            // commented out because the code does not explicitly reference the names (but they are logically defined).  
            // MultiByte2 = 0xD,   // 2 bytes follow (we don't bother with the nibble optimzation
            // MultiByte3 = 0xE,   // 3 bytes follow (we don't bother with the nibble optimzation
            // MultiByte4 = 0xF,   // 4 bytes follow (we don't bother with the nibble optimzation
        }

        private static Guid AdoNetProvider = new Guid("6a4dfe53-eb50-5332-8473-7b7e10a94fd1");      // We process these specially (unfortunately). 

        /// <summary>
        /// We don't do a stop all processing associated with the stop event is done.  Thus if we are not 'on'
        /// the stop event, then you can do any deferred processing.  
        /// </summary>
        public void DoStopIfNecessary(bool force = true)
        {
            // If we are not exactly on the StopEvent then do the deferred stop before doing any processing.  
            if (m_deferredStop != null && (force || m_deferredStop.StopEventIndex != m_source.CurrentEventIndex))
            {
                var startStopActivity = m_deferredStop;
                m_traceActivityToStartStopActivity.Set(startStopActivity.activityIndex, startStopActivity.Creator);
                m_activeActivities.Remove(startStopActivity.key);
                m_activeActivitiesByActivityId.Remove(startStopActivity.ActivityID);

                startStopActivity.key = null;      // This also marks the activity as truly stopped.  
                m_deferredStop = null;

#if false        // TODO FIX NOW remove after debugging complete. 
                var data = m_source.TraceLog.GetEvent(m_source.CurrentEventIndex);
                activity = activity.Creator;
                var thread = data.Thread();

                Trace.WriteLine(string.Format("{0,10:n3}: Thread {1} NumActive {2} Task {3}\r\n    EXECUTING-STOP {4}",
                    data.TimeStampRelativeMSec, thread != null ? thread.ThreadID : -1, m_activeActivities.Count, activity, activity != null ? activity.ActivityPathString : ""));
#endif

                // Issue callback if requested AFTER state update
                var onStartStop = OnStartOrStop;    
                if (onStartStop != null)
                    onStartStop(startStopActivity, false);
            }
        }
        StartStopActivity m_deferredStop;
        TraceLogEventSource m_source;
        ActivityComputer m_taskComputer;                                            // I need to be able to get the current Activity
        GrowableArray<StartStopActivity> m_traceActivityToStartStopActivity;        // Maps a trace activity to a start stop activity. 
        Dictionary<StartStopKey, StartStopActivity> m_activeActivities;             // Lookup activities by start-stop key.   
        Dictionary<Guid, StartStopActivity> m_activeActivitiesByActivityId;         // Lookup activities by their Activity ID.  
        #endregion
    }

    /// <summary>
    /// A StartStop reresents an activity between a start and stop event as generated by EvetSource.  
    /// </summary>
    [Obsolete("Not Obsolete but experimental.  This may change in future releases.")]
    public class StartStopActivity
    {
        /// <summary>
        /// The name of the activity (The Task name for the start-stop event as well as the activity ID)
        /// </summary>
        public string Name
        {
            get
            {
                string activityString = StartStopActivityComputer.ActivityPathString(ActivityID);
                if (!activityString.StartsWith("//"))
                {
                    if (activityString.EndsWith("-08090a0b0c0d"))   // We make up fake activity ids (see StartStopActivityComputer ctor) for HttpRequest and SQL Command)
                        activityString = "SQL/Id=" + activityString.Substring(0, 8);  // The first 8 bytes is the ID that links the start and stop.  
                    else
                        activityString = "@" + activityString;                    // Simply use the GUID as the marker.  
                }
                if (ExtraInfo == null)
                    return TaskName + "(" + activityString + ")";
                else
                    return TaskName + "(" + activityString + "," + ExtraInfo + ")";
            }
        }
        /// <summary>
        /// If the activity has additional information associated with it (e.g. a URL), put it here.  Can be null.
        /// </summary>
        public string ExtraInfo { get; private set; }
        /// <summary>
        /// The Task name (the name prefix that is common to both the start and stop event
        /// </summary>
        public string TaskName { get; private set; }
        /// <summary>
        /// The processID associated with this activity
        /// </summary>
        public int ProcessID { get; private set; }
        /// <summary>
        /// The Activity ID (as a GUID) that matches the start and stop together. 
        /// </summary>
        public Guid ActivityID { get; private set; }
        /// <summary>
        /// The path of creators that created this activity.  
        /// </summary>
        public string ActivityPathString
        {
            get
            {
                var ret = Name;
                if (Creator != null)
                    ret = Creator.ActivityPathString + " " + ret;
                return ret;
            }
        }
        /// <summary>
        /// The start-stop activity that created this activity (thus it makes a tree)
        /// </summary>
        public StartStopActivity Creator { get; private set; }
        /// <summary>
        /// The TraceLog event Index, of the start event (you can get addition info)
        /// </summary>
        public EventIndex StartEventIndex { get; private set; }
        /// <summary>
        /// The TraceLog event Index, of the stop event (you can get addition info)
        /// </summary>
        public EventIndex StopEventIndex { get; private set; }
        /// <summary>
        /// The time in MSec from the start of the trace when the start event happened. 
        /// </summary>
        public double StartTimeRelativeMSec { get; private set; }
        /// <summary>
        /// The duration of activity in MSec (diff between stop and start)
        /// </summary>
        public double DurationMSec { get; private set; }
        /// <summary>
        /// This activity has completed (the Stop event has been received).  Thus Duration is valid.
        /// </summary>
        public bool IsStopped { get { return DurationMSec != 0 && key == null; } }
        /// <summary>
        /// Returns a stack on the outputStackSource which has a frame for each activity that
        /// caused this activity, as well as the root of the given 'rootStack' (often a stack representing the process).    
        /// </summary>
        public StackSourceCallStackIndex GetActivityStack(MutableTraceEventStackSource outputStackSource, StackSourceCallStackIndex rootStack)
        {
            StackSourceCallStackIndex stackIdx = rootStack;
            if (Creator != null)
                stackIdx = Creator.GetActivityStack(outputStackSource, stackIdx);

            // Add my name to the list of frames.  
            stackIdx = outputStackSource.Interner.CallStackIntern(outputStackSource.Interner.FrameIntern("Activity " + Name), stackIdx);
            return stackIdx;
        }
        #region private
        /// <summary>
        /// override.   Gives the name and start time.  
        /// </summary>
        public override string ToString()
        {
            return "StartStopActivity(" + Name + ", Start=" + StartTimeRelativeMSec.ToString("n3") + ")";
        }

        internal StartStopActivity(TraceEvent startEvent, string taskName, ref Guid activityID, StartStopActivity creator, string extraInfo = null)
        {
            ProcessID = startEvent.ProcessID;
            ActivityID = activityID;
            StartTimeRelativeMSec = startEvent.TimeStampRelativeMSec;
            StartEventIndex = startEvent.EventIndex;
            TaskName = taskName;
            Creator = creator;
            ExtraInfo = extraInfo;

            // generate a ID that makes this unique among all children of the creator. 
            if (creator == null)
                myChildID = (++s_nextChildID);
            else
                myChildID = (++creator.nextChildID);
        }

        /// <summary>
        /// We don't update the state for the stop at the time of the stop, but at the next call to any of the StartStopActivityComputer APIs.  
        /// </summary>
        internal void RememberStop(EventIndex stopEventIndex, double stopTimeRelativeMSec, StartStopKey key, int activityIndex)
        {
            if (DurationMSec == 0)
            {
                this.key = key;
                this.activityIndex = activityIndex;
                this.StopEventIndex = stopEventIndex;
                this.DurationMSec = stopTimeRelativeMSec - StartTimeRelativeMSec;
            }
        }

        static int s_nextChildID;       // Used to generate small IDs 
        private int myChildID;
        private int nextChildID;

        // these are used to implement deferred stops.  
        internal StartStopKey key;      // The key that links up the start and stop for this activity. 
        internal int activityIndex;         // the index for the task that was active at the time of the stop.  
        #endregion
    };

    #region private classes
    /// <summary>
    /// The key used to correlate start and stop events;
    /// </summary>
    internal class StartStopKey : IEquatable<StartStopKey>
    {
        public StartStopKey(Guid provider, TraceEventTask task, int processId, Guid activityID)
        {
            this.ProcessId = processId;
            this.Provider = provider;
            this.task = task;
            this.ActivityId = activityID;
        }
        public int ProcessId;
        public Guid Provider;
        public Guid ActivityId;
        public TraceEventTask task;

        public override int GetHashCode()
        {
            return Provider.GetHashCode() + ActivityId.GetHashCode() + (int)task + ProcessId;
        }

        public bool Equals(StartStopKey other)
        {
            return other.ProcessId == ProcessId && other.Provider == Provider && other.ActivityId == ActivityId && other.task == task;
        }

        public override bool Equals(object obj) { throw new NotImplementedException(); }

        public override string ToString()
        {
            return "<Key  Task=\"" + ((int)task) + "\" ActivityId=\"" + ActivityId + "\" Provider=\"" + Provider + "\" Process=\"" + ProcessId + "\">";
        }
    }
    #endregion
}