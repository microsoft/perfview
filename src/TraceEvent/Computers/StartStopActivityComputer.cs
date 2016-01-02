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
    /// Calculates start-stop activities (computes duration),  It uses the 'standard' mechanism of using 
    /// ActivityIDs to corelate the start and stop (and any other events between the start and stop, 
    /// and use the RelatedActivityID on START events to indicate the creator of the activity, so you can
    /// form nested start-stop activities.   
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
            m_activeStartStopActivitiesByActivityId = new Dictionary<Guid, StartStopActivity>();
            m_taskComputer = taskComputer;

            // Whenever a new Activity is created, propagate the start-stop activity from the creator
            // to the created task.  
            taskComputer.Create += delegate (TraceActivity activity, TraceEvent data)
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
            Guid[] threadToLastAspNetGuid = new Guid[m_source.TraceLog.Threads.Count];     // Only need to fix up ASPNet activity ids.  

            var aspNetParser = new AspNetTraceEventParser(m_source);
            aspNetParser.AspNetReqStart += delegate (AspNetStartTraceData data)
            {
                StartStopKey key = new StartStopKey(data.ProviderGuid, ((TraceEventTask)1), data.ProcessID, data.ContextId);

                // if the related activity is not present, try using the context ID as the creator ID to look up.   
                // these events reuse the IIS which means first stop kills everything.   As it turns out
                // IIS stops before ASP (also incorrect) but it mostly is benign...  
                StartStopActivity creator = null;
                if (data.RelatedActivityID == Guid.Empty)
                    m_activeStartStopActivitiesByActivityId.TryGetValue(data.ContextId, out creator);

                OnStart(key, data.Path, data, null, creator);
            };
            aspNetParser.AspNetReqStop += delegate (AspNetStopTraceData data)
            {
                StartStopKey key = new StartStopKey(data.ProviderGuid, ((TraceEventTask)1), data.ProcessID, data.ContextId);
                OnStop(key, data);
            };

#if false       // TODO FIX NOW use or remove  
            aspNetParser.AspNetReqStartHandler += delegate (AspNetStartHandlerTraceData data)
            {
                SetThreadToStartStop(data.ContextId, data);
            };
            aspNetParser.AspNetReqPipelineModuleEnter += delegate (AspNetPipelineModuleEnterTraceData data)
            {
                SetThreadToStartStop(data.ContextId, data);
            };
            aspNetParser.AspNetReqGetAppDomainEnter += delegate (AspNetGetAppDomainEnterTraceData data)
            {
                SetThreadToStartStop(data.ContextId, data);
            };
#endif 

            dynamicParser.All += delegate (TraceEvent data)
            {
                // Special case IIS.  It does not use start and stop opcodes (Ugg), but otherwise is a reasonble 
                if (data.ID <= (TraceEventID) 2 && data.ProviderGuid == MicrosoftWindowsIISProvider)
                {
                    StartStopKey key = new StartStopKey(data.ProviderGuid, data.Task, data.ProcessID, data.ActivityID);
                    if (data.ID == (TraceEventID) 1)
                    {
                        string extraStartInfo = data.PayloadByName("RequestURL") as string;
                        OnStart(key, extraStartInfo, data, null, null, "IISRequest");
                    }
                    else if (data.ID == (TraceEventID) 2)
                        OnStop(key, data);
                }

                // TODO decide what the correct heuristic is.  
                // Currently I only do this for things that might be an EventSoruce 
                if (!TraceEventProviders.MaybeAnEventSource(data.ProviderGuid))
                    return;

                if (data.Opcode != TraceEventOpcode.Start && data.Opcode != TraceEventOpcode.Stop)
                {
                    // In V4.6 the activity ID for Microsoft-Windows-ASPNET/Request/Start is improperly set, but we can fix it by 
                    // looking at the 'send' event that happens just before it.   Can be removed when V4.6 no longer deployed.  
                    // TODO remove (including threadToLastAspNetGuid) after 9/2016
                    if (data.Opcode == (TraceEventOpcode)9 && data.ProviderGuid == MicrosoftWindowsASPNetProvider)
                    {
                        TraceThread thread = data.Thread();
                        if (thread != null)
                            threadToLastAspNetGuid[(int)thread.ThreadIndex] = data.RelatedActivityID;
                    }

                    // These providers are weird in that they don't event do start and stop opcodes.  
                    else if (data.Opcode == TraceEventOpcode.Info && data.ProviderGuid == AdoNetProvider)
                        FixAndProcessAdoNetEvents(data);
                    return;
                }

                if (data.ProviderGuid == FrameworkEventSourceTraceEventParser.ProviderGuid)
                    FixAndProcessFrameworkEvents(data);
                else if (data.ProviderGuid == MicrosoftWindowsASPNetProvider)
                    FixAndProcessWindowsASP(data, threadToLastAspNetGuid);
                else // Normal case EventSource Start-Stop events that follow proper conventions.  
                {
                    Guid activityID = data.ActivityID;
                    if (StartStopActivityComputer.IsActivityPath(activityID))
                    {
                        StartStopKey key = new StartStopKey(data.ProviderGuid, data.Task, data.ProcessID, activityID);
                        if (data.Opcode == TraceEventOpcode.Start)
                        {
                            string extraStartInfo = null;
                            // Include the first argument if it is a string
                            if (0 < data.PayloadNames.Length)
                            {
                                try { extraStartInfo = data.PayloadValue(0) as string; }
                                catch (Exception) { }
                                if (extraStartInfo != null)
                                    extraStartInfo = data.payloadNames[0] + "=" + extraStartInfo;
                            }
                            OnStart(key, extraStartInfo, data);
                        }
                        else
                        {
                            Debug.Assert(data.Opcode == TraceEventOpcode.Stop);
                            OnStop(key, data);
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
        public StartStopActivity GetCurrentStartStopActivity(TraceThread thread, TraceEvent context = null)
        {
            DoStopIfNecessary();        // Do any deferred stops. 

            // Search up the stack of tasks, seeing if we have a start-stop activity. 
            TraceActivity curTaskActivity = m_taskComputer.GetCurrentActivity(thread);
            if (curTaskActivity == null)
                return null;

            int taskIndex = (int)curTaskActivity.Index;
            StartStopActivity ret = m_traceActivityToStartStopActivity.Get(taskIndex);

            if (ret == null && context != null)
            {
                Guid activityID = context.ActivityID;
                if (activityID != Guid.Empty)
                    m_activeStartStopActivitiesByActivityId.TryGetValue(activityID, out ret);
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
        /// Gets the current Start-Stop activity for a given TraceActivity.  
        /// </summary>
        /// <param name="curActivity"></param>
        /// <returns></returns>
        public StartStopActivity GetStartStopActivityForActivity(TraceActivity curActivity)
        {
            int taskIndex = (int)curActivity.Index;
            StartStopActivity ret = m_traceActivityToStartStopActivity.Get(taskIndex);

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

        // Events 
        /// <summary>
        /// If set, called AFTER a Start-Stop activity starts, called with the activity and the event that caused the start. 
        /// </summary>
        public Action<StartStopActivity, TraceEvent> Start;
        /// <summary>
        /// If set, called BEFORE a Start-Stop activity stops, called with the activity and the event that caused the start. 
        /// </summary>
        public Action<StartStopActivity, TraceEvent> Stop;

        // TODO decide if we need this...
        // public Action<StartStopActivity> AfterStop;

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
        static readonly Guid MicrosoftWindowsIISProvider = new Guid("de4649c9-15e8-4fea-9d85-1cdda520c334");

        void FixAndProcessAdoNetEvents(TraceEvent data)
        {
            Debug.Assert(data.ProviderGuid == AdoNetProvider);
            if (data.ID == (TraceEventID)1 || data.ID == (TraceEventID)2) // BeginExecute || EndExecute
            {
                Debug.Assert(data.payloadNames[0] == "objectId");
                Guid startStopId = new Guid((int)data.PayloadValue(0), 1234, 5, 7, 7, 8, 9, 10, 11, 12, 13);  // Tail 7,7,8,9,10,11,12,13 means SQL Execute
                TraceEventTask executeTask = (TraceEventTask)1;

                if (data.ID == (TraceEventID)1) // BeginExecute
                {
                    // TODO as of 2/2015 there was a bug where the ActivityPath logic was getting this ID wrong, so we get to from the thread. 
                    // relatedActivityID = activityID;
                    TraceThread thread = data.Thread();
                    if (thread == null)
                        return;
                    StartStopActivity creator = GetCurrentStartStopActivity(thread, data);

                    Debug.Assert(data.payloadNames[1] == "dataSource");
                    var extraStartInfo = data.PayloadValue(1) as string;
                    StartStopKey key = new StartStopKey(data.ProviderGuid, executeTask, data.ProcessID, startStopId);
                    OnStart(key, extraStartInfo, data, thread, creator, "SQLCommand");
                    return;
                }
                else
                {
                    Debug.Assert(data.ID == (TraceEventID)2); // EndExecute
                    StartStopKey key = new StartStopKey(data.ProviderGuid, executeTask, data.ProcessID, startStopId);
                    OnStop(key, data);
                    return;
                }
            }
        }
        void FixAndProcessFrameworkEvents(TraceEvent data)
        {
            Debug.Assert(data.ProviderGuid == FrameworkEventSourceTraceEventParser.ProviderGuid);
            Debug.Assert(data.Opcode == TraceEventOpcode.Start || data.Opcode == TraceEventOpcode.Stop);

            if (data.Task == (TraceEventTask)1 || data.Task == (TraceEventTask)2) // GetResponse or GetResponseStream
            {
                Debug.Assert(data.payloadNames[0] == "id");
                long id = (long)data.PayloadValue(0);
                Guid startStopId = new Guid((int)id, (short)(id >> 32), (short)(id >> 48), 6, 7, 8, 9, 10, 11, 12, 13);   // Tail 6,7,8,9,10,11,12,13 means GetResponse
                StartStopKey key = new StartStopKey(data.ProviderGuid, data.Task, data.ProcessID, startStopId);

                if (data.Opcode == TraceEventOpcode.Start)
                {
                    // TODO as of 2/2015 there was a bug where the ActivityPath logic was getting this ID wrong, so we get to from the thread. 
                    // relatedActivityID = activityID;
                    TraceThread thread = data.Thread();
                    if (thread == null)
                        return;
                    StartStopActivity creator = GetCurrentStartStopActivity(thread, data);

                    string taskName = "Http" + data.TaskName;
                    Debug.Assert(data.payloadNames[1] == "uri");
                    string url = data.PayloadValue(1) as string;
                    OnStart(key, url, data, thread, creator, taskName);
                }
                else
                {
                    Debug.Assert(data.Opcode == TraceEventOpcode.Stop);
                    OnStop(key, data);
                }
            }
        }

        /// <summary>
        /// fix ASP.NET receiving events  
        /// </summary>
        void FixAndProcessWindowsASP(TraceEvent data, Guid[] threadToLastAspNetGuid)
        {
            Debug.Assert(data.ProviderGuid == MicrosoftWindowsASPNetProvider);
            Debug.Assert(data.Opcode == TraceEventOpcode.Start || data.Opcode == TraceEventOpcode.Stop);

            TraceThread thread = data.Thread();
            if (thread == null)
                return;

            if (data.Opcode == TraceEventOpcode.Start)
            {
                string taskName = "RecHttp" + data.TaskName;
                string extraStartInfo = (string)data.PayloadValue(1);        // This is the URL

                Guid activityID = data.ActivityID;
                StartStopKey key = new StartStopKey(data.ProviderGuid, data.Task, data.ProcessID, activityID);

                // In V4.6 we use a ActivityPath but this is a bug in that the 'STOP will not necessarily match it. 
                // In V4.7 and beyond this is fixed (thus the ID will NOT be a ActivityPath).  
                // Thus this can be removed after V4.6 is aged out (e.g. 9/2016)
                // To fix V4.6 we set the ID 
                StartStopKey fixedKey = null;
                if (StartStopActivityComputer.IsActivityPath(activityID))
                {
                    Guid fixedActivityID = threadToLastAspNetGuid[(int)thread.ThreadIndex];
                    if (fixedActivityID != Guid.Empty)
                    {
                        if (fixedActivityID != activityID)
                        {
                            // Before we make the fixed key official, remember the 'bad' key because we need to add this to the 
                            // lookup table because the STOP may use it.   
                            fixedKey = new StartStopKey(data.ProviderGuid, data.Task, data.ProcessID, activityID);
                            // Also add the old activity path to the extraStartInfo, which is needed ServiceProfiler versioning between the monitor and the detailed file.  
                            extraStartInfo = StartStopActivityComputer.ActivityPathString(activityID) + "," + extraStartInfo;
                            activityID = fixedActivityID;       // Make the fixed key the 'offiical' one.
                        }
                    }
                    else
                        Trace.WriteLine(data.TimeStampRelativeMSec.ToString("n3") + " Could not find ASP.NET Send event to fix Start event");
                }

                OnStart(key, extraStartInfo, data, thread, null, taskName);

                // in V4.6 the Stop may use the fixed or unfixed ID, so put both IDs into the lookup table.  
                // Note that this leaks table entries, but this 
                if (fixedKey != null)
                    m_activeActivities[fixedKey] = m_activeActivities[key];
            }
            else
            {
                Debug.Assert(data.Opcode == TraceEventOpcode.Stop);
                StartStopKey key = new StartStopKey(data.ProviderGuid, data.Task, data.ProcessID, data.ActivityID);
                OnStop(key, data);
            }
            threadToLastAspNetGuid[(int)thread.ThreadIndex] = Guid.Empty;
        }

        private void OnStart(StartStopKey key, string extraStartInfo, TraceEvent data, TraceThread thread = null, StartStopActivity creator = null, string taskName = null)
        {
            // Because we want the stop to logically be 'after' the actual stop event (so that the stop looks like
            // it is part of the start-stop activity we defer it until the next event.   If there is already
            // a deferral, we can certainly do that one now.  
            DoStopIfNecessary();

            if (thread == null)
            {
                thread = data.Thread();
                if (thread == null)
                    return;
            }
            TraceActivity curTaskActivity = m_taskComputer.GetCurrentActivity(thread);
            ActivityIndex taskIndex = curTaskActivity.Index;

            if (creator == null)
            {
                if (data.RelatedActivityID != Guid.Empty)
                {
                    m_activeStartStopActivitiesByActivityId.TryGetValue(data.RelatedActivityID, out creator);
                    if (creator == null)
                        Trace.WriteLine(data.TimeStampRelativeMSec.ToString("n3") + " Warning: Could not find creator Activity " + StartStopActivityComputer.ActivityPathString(data.RelatedActivityID));
                }
            }

            if (taskName == null)
                taskName = data.TaskName;

            // Create the new activity.  
            StartStopActivity activity = new StartStopActivity(data, taskName, ref key.StartStopId, creator, m_nextIndex++, extraStartInfo);
            if (creator != null)
                creator.LiveChildCount++;
            m_activeActivities[key] = activity;                              // So we can correlate stops with this start.   

            // Remember that this task is doing this activity.              // So we can transfer this activity to any subsequent events on this Task/Thread.  
            m_activeStartStopActivitiesByActivityId[key.StartStopId] = activity;     // So we can look it up (for children's related Activity IDs)
            m_traceActivityToStartStopActivity.Set((int)taskIndex, activity);

            // Issue callback if requested AFTER state update
            var onStartAfter = Start;
            if (onStartAfter != null)
                onStartAfter(activity, data);
        }

#if false
        void SetThreadToStartStop(Guid startStopActivityID, TraceEvent data)
        {
            // Because we want the stop to logically be 'after' the actual stop event (so that the stop looks like
            // it is part of the start-stop activity we defer it until the next event.   If there is already
            // a deferral, we can certainly do that one now.  
            DoStopIfNecessary();

            // Policy, right now we don't let this auto-start events we simply give up if we can't find a 
            // active start-stop activity.  
            StartStopActivity startStopActivity;
            if (!m_activeStartStopActivitiesByActivityId.TryGetValue(startStopActivityID, out startStopActivity))
                return;

            TraceThread thread = data.Thread();
            if (thread == null)
                return;
            TraceActivity curTaskActivity = m_taskComputer.GetCurrentActivity(thread);
            ActivityIndex taskIndex = curTaskActivity.Index;

            StartStopActivity activity;
            // Find the corresponding start event.  
            if (m_activeActivities.TryGetValue(key, out activity))
            {
                // We defer the stop until the NEXT event (so that stops look like they are IN the activity). 
                // So here we just capture what is needed to do this later (basically on the next event).  
                activity.RememberStop(data.EventIndex, data.TimeStampRelativeMSec, key, taskIndex);
                m_deferredStop = activity;      // Remember this one for deferral.  

                // Issue callback if requested AFTER state update
                var onStartBefore = Stop;
                if (onStartBefore != null)
                    onStartBefore(activity, data);
            }
    }
#endif

        private void OnStop(StartStopKey key, TraceEvent data)
        {
            // Because we want the stop to logically be 'after' the actual stop event (so that the stop looks like
            // it is part of the start-stop activity we defer it until the next event.   If there is already
            // a deferral, we can certainly do that one now.  
            DoStopIfNecessary();

            TraceThread thread = data.Thread();
            if (thread == null)
                return;
            TraceActivity curTaskActivity = m_taskComputer.GetCurrentActivity(thread);
            ActivityIndex taskIndex = curTaskActivity.Index;

            StartStopActivity activity;
            // Find the corresponding start event.  
            if (m_activeActivities.TryGetValue(key, out activity))
            {
                // We defer the stop until the NEXT event (so that stops look like they are IN the activity). 
                // So here we just capture what is needed to do this later (basically on the next event).  
                activity.RememberStop(data.EventIndex, data.TimeStampRelativeMSec, key, taskIndex);
                m_deferredStop = activity;      // Remember this one for deferral.  

                // Issue callback if requested AFTER state update
                var onStartBefore = Stop;
                if (onStartBefore != null)
                    onStartBefore(activity, data);
            }
            else
            {
                // TODO GetResponseStream Stops can sometime occur before the start (if they can be accomplished without I/O).  
                if (!(data.ProviderGuid == FrameworkEventSourceTraceEventParser.ProviderGuid))
                    Trace.WriteLine("Warning: Unmatched stop at " + data.TimeStampRelativeMSec.ToString("n3") + " key = " + key);
            }
        }

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
        private void DoStopIfNecessary()
        {
            // If we are not exactly on the StopEvent then do the deferred stop before doing any processing.  
            if (m_deferredStop != null && m_deferredStop.StopEventIndex != m_source.CurrentEventIndex)
            {
                var startStopActivity = m_deferredStop;
                m_traceActivityToStartStopActivity.Set((int)startStopActivity.activityIndex, startStopActivity.Creator);
                m_activeActivities.Remove(startStopActivity.key);
                m_activeStartStopActivitiesByActivityId.Remove(startStopActivity.ActivityID);

                var creator = startStopActivity.Creator;
                if (creator != null)
                {
                    --creator.LiveChildCount;
                    Debug.Assert(0 <= creator.LiveChildCount);
                }

                startStopActivity.key = null;      // This also marks the activity as truly stopped.  
                m_deferredStop = null;
            }
        }
        StartStopActivity m_deferredStop;
        TraceLogEventSource m_source;
        ActivityComputer m_taskComputer;                                             // I need to be able to get the current Activity
        GrowableArray<StartStopActivity> m_traceActivityToStartStopActivity;         // Maps a trace activity to a start stop activity at the current time. 
        Dictionary<StartStopKey, StartStopActivity> m_activeActivities;              // Lookup activities by start-stop key.   
        // TODO consolidate m_activeActivities m_activeActivitiesm_activeActivities and m_activeStartStopActivitiesByActivityId   
        Dictionary<Guid, StartStopActivity> m_activeStartStopActivitiesByActivityId; // Lookup activities by their Activity ID, needed to find creator from RelativeActivityID.  
        int m_nextIndex;
#endregion
    }

    /// <summary>
    /// An dense number that defines the identity of a StartStopActivity.  Used to create side arrays 
    /// for StartStopActivity info.  
    /// </summary>
    public enum StartStopActivityIndex
    {
        /// <summary>
        /// An illegal index, sutable for a sentinal.  
        /// </summary>
        Illegal = -1,
    }

    /// <summary>
    /// A StartStop reresents an activity between a start and stop event as generated by EvetSource.  
    /// </summary>
    [Obsolete("Not Obsolete but experimental.  This may change in future releases.")]
    public class StartStopActivity
    {
        /// <summary>
        /// The index (small dense numbers suitabilty for array indexing) for this activity. 
        /// </summary>
        public StartStopActivityIndex Index { get; private set; }
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
                    if (activityString.EndsWith("0607-08090a0b0c0d"))   // Http Command)
                        activityString = "HTTP/Id=" + activityString.Substring(0, 8); // The first bytes are the ID that links the start and stop.
                    if (activityString.EndsWith("0707-08090a0b0c0d"))   // SQL Command)
                        activityString = "SQL/Id=" + activityString.Substring(0, 8);  // The first 8 bytes is the ID that links the start and stop.
                }

                string ret;
                if (ExtraInfo == null)
                    ret = TaskName + "(" + activityString + ")";
                else
                    ret = TaskName + "(" + activityString + "," + ExtraInfo + ")";
                return ret;
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
        /// <summary>
        /// returns the number of children that are alive.  
        /// </summary>
        public int LiveChildCount { get; internal set; }
#region private
        /// <summary>
        /// override.   Gives the name and start time.  
        /// </summary>
        public override string ToString()
        {
            return "StartStopActivity(" + Name + ", Start=" + StartTimeRelativeMSec.ToString("n3") + ")";
        }

        internal StartStopActivity(TraceEvent startEvent, string taskName, ref Guid activityID, StartStopActivity creator, int index, string extraInfo = null)
        {
            ProcessID = startEvent.ProcessID;
            ActivityID = activityID;
            StartTimeRelativeMSec = startEvent.TimeStampRelativeMSec;
            StartEventIndex = startEvent.EventIndex;
            TaskName = taskName;
            Creator = creator;
            ExtraInfo = extraInfo;
            Index = (StartStopActivityIndex)index;

            // generate a ID that makes this unique among all children of the creator. 
            if (creator == null)
                myChildID = (++s_nextChildID);
            else
                myChildID = (++creator.nextChildID);
        }

        /// <summary>
        /// We don't update the state for the stop at the time of the stop, but at the next call to any of the StartStopActivityComputer APIs.  
        /// </summary>
        internal void RememberStop(EventIndex stopEventIndex, double stopTimeRelativeMSec, StartStopKey key, ActivityIndex activityIndex)
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
        internal StartStopKey key;                // The key that links up the start and stop for this activity. 
        internal ActivityIndex activityIndex;     // the index for the task that was active at the time of the stop.  
#endregion
    };

#region private classes
    /// <summary>
    /// The key used to correlate start and stop events;
    /// </summary>
    internal class StartStopKey : IEquatable<StartStopKey>
    {
        public StartStopKey(Guid provider, TraceEventTask task, int processId, Guid startStopId)
        {
            this.ProcessId = processId;
            this.Provider = provider;
            this.task = task;
            this.StartStopId = startStopId;
        }
        public int ProcessId;
        public Guid Provider;
        public Guid StartStopId;
        public TraceEventTask task;

        public override int GetHashCode()
        {
            return Provider.GetHashCode() + StartStopId.GetHashCode() + (int)task + ProcessId;
        }

        public bool Equals(StartStopKey other)
        {
            return other.ProcessId == ProcessId && other.Provider == Provider && other.StartStopId == StartStopId && other.task == task;
        }

        public override bool Equals(object obj) { throw new NotImplementedException(); }

        public override string ToString()
        {
            return "<Key  Task=\"" + ((int)task) + "\" ActivityId=\"" + StartStopId + "\" Provider=\"" + Provider + "\" Process=\"" + ProcessId + "\">";
        }
    }
#endregion
}