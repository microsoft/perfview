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
using StartStopKey = System.Guid;   // The start-stop key is unique in the trace.  We incorperate the process as well as activity ID to achieve this.

namespace Microsoft.Diagnostics.Tracing
{
    /// <summary>
    /// Calculates start-stop activities (computes duration),  It uses the 'standard' mechanism of using 
    /// ActivityIDs to corelate the start and stop (and any other events between the start and stop, 
    /// and use the RelatedActivityID on START events to indicate the creator of the activity, so you can
    /// form nested start-stop activities.   
    /// </summary>
    unsafe public class StartStopActivityComputer
    {
        /// <summary>
        /// Create a new ServerRequest Computer.
        /// </summary>
        public StartStopActivityComputer(TraceLogEventSource source, ActivityComputer taskComputer)
        {
            taskComputer.NoCache = true;            // Can't cache start-stops (at the moment)
            m_source = source;
            m_activeStartStopActivities = new Dictionary<StartStopKey, StartStopActivity>();
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

            // Only need to fix up V4.6 Windows-ASP activity ids.   It can be removed after we
            // don't care about V4.6 runtimes (since it is fixed in V4.6.2 and beyond). 
            // It basicaly remembers the related activity ID of the last RequestSend event on
            // each thread, which we use to fix the activity ID of the RequestStart event 
            Guid[] threadToLastAspNetGuid = new Guid[m_source.TraceLog.Threads.Count];     

            var dynamicParser = source.Dynamic;
            dynamicParser.All += delegate (TraceEvent data)
            {
                // Special case IIS.  It does not use start and stop opcodes (Ugg), but otherwise 
                // follows normal start-stop activity ID conventions.   We also want it to work even
                // though it is not a EventSource.  
                if (data.ID <= (TraceEventID)2 && data.ProviderGuid == MicrosoftWindowsIISProvider)
                {
                    if (data.ID == (TraceEventID)1)
                    {
                        string extraStartInfo = data.PayloadByName("RequestURL") as string;
                        OnStart(data, extraStartInfo, null, null, null, "IISRequest");
                    }
                    else if (data.ID == (TraceEventID)2)
                        OnStop(data);
                }

                // TODO decide what the correct heuristic for deciding what start-stop events are interesting.  
                // Currently I only do this for things that might be an EventSource 
                if (!TraceEventProviders.MaybeAnEventSource(data.ProviderGuid))
                    return;

                // Try to filter out things quickly.   We really only care about start and stop events 
                // (except in special cases where the conventions were not followed and we fix them up). 
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
                    // These providers are weird in that they don't event do start and stop opcodes.  This is unfortunate.  
                    else if (data.Opcode == TraceEventOpcode.Info && data.ProviderGuid == AdoNetProvider)
                        FixAndProcessAdoNetEvents(data);
                    return;
                }

                // OK so now we only have EventSources with start and stop opcodes.   THere are a few that don't follow
                // conventions completely, and then we handle the 'normal' case 
                if (data.ProviderGuid == FrameworkEventSourceTraceEventParser.ProviderGuid)
                    FixAndProcessFrameworkEvents(data);
                else if (data.ProviderGuid == MicrosoftWindowsASPNetProvider)
                    FixAndProcessWindowsASP(data, threadToLastAspNetGuid);
                else // Normal case EventSource Start-Stop events that follow proper conventions.  
                {
                    // We currently only handle Start-Stops that use the ActivityPath convention
                    // We could change this, but it is not clear what value it has to do that.  
                    Guid activityID = data.ActivityID;
                    if (StartStopActivityComputer.IsActivityPath(activityID))
                    {
                        if (data.Opcode == TraceEventOpcode.Start)
                        {
                            string extraStartInfo = null;
                            // Include the first argument in extraInfo if it is a string (e.g. a URL or other identifier).  
                            if (0 < data.PayloadNames.Length)
                            {
                                try { extraStartInfo = data.PayloadValue(0) as string; }
                                catch (Exception) { }
                                if (extraStartInfo != null)
                                    extraStartInfo = data.payloadNames[0] + "=" + extraStartInfo;
                            }
                            OnStart(data, extraStartInfo);
                        }
                        else
                        {
                            Debug.Assert(data.Opcode == TraceEventOpcode.Stop);
                            OnStop(data);
                        }
                    }
                    else
                        Trace.WriteLine("Skipping start at  " + data.TimeStampRelativeMSec.ToString("n3") + " name = " + data.EventName);
                }
            };

            var aspNetParser = new AspNetTraceEventParser(m_source);
            aspNetParser.AspNetReqStart += delegate (AspNetStartTraceData data)
            {
                // if the related activity is not present, try using the context ID as the creator ID to look up.   
                // The ASPNet events reuse the IIS ID which means first stop kills both.   As it turns out
                // IIS stops before ASP (also incorrect) but it mostly this is benign...  
                StartStopActivity creator = null;
                if (data.RelatedActivityID == Guid.Empty)
                    creator = GetActiveStartStopActivityTable(data.ContextId, data.ProcessID);

                Guid activityId = data.ContextId;
                OnStart(data, data.Path, &activityId, null, creator);
            };
            aspNetParser.AspNetReqStop += delegate (AspNetStopTraceData data)
            {
                Guid activityId = data.ContextId;
                OnStop(data, &activityId);
            };

            // There are other ASP.NET events that have context information and this is useful
            aspNetParser.AspNetReqStartHandler += delegate (AspNetStartHandlerTraceData data)
            {
                SetThreadToStartStopActivity(data, data.ContextId);
            };
            aspNetParser.AspNetReqPipelineModuleEnter += delegate (AspNetPipelineModuleEnterTraceData data)
            {
                SetThreadToStartStopActivity(data, data.ContextId);
            };
            aspNetParser.AspNetReqGetAppDomainEnter += delegate (AspNetGetAppDomainEnterTraceData data)
            {
                SetThreadToStartStopActivity(data, data.ContextId);
            };
        }
        /// <summary>
        /// The current start-stop activity on the given thread.   
        /// If present 'context' is used to look up the current activityID and try to use that to repair missing Starts.  
        /// Basically if we can't figure out what StartStop activity the thread from just the threadID we can use the activityID 
        /// from the 'context' event to find it as a backup.     
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
                if (context.ActivityID != Guid.Empty)
                    ret = GetActiveStartStopActivityTable(context.ActivityID, context.ProcessID);
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
        private static readonly Guid MicrosoftWindowsASPNetProvider = new Guid("ee799f41-cfa5-550b-bf2c-344747c1c668");
        private static readonly Guid MicrosoftWindowsIISProvider = new Guid("de4649c9-15e8-4fea-9d85-1cdda520c334");
        private static readonly Guid AdoNetProvider = new Guid("6a4dfe53-eb50-5332-8473-7b7e10a94fd1");     

        // The main start and stop logic.  
        unsafe private void OnStart(TraceEvent data, string extraStartInfo = null, Guid* activityId = null, TraceThread thread = null, StartStopActivity creator = null, string taskName = null)
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
                    creator = GetActiveStartStopActivityTable(data.RelatedActivityID, data.ProcessID);
                    if (creator == null)
                        Trace.WriteLine(data.TimeStampRelativeMSec.ToString("n3") + " Warning: Could not find creator Activity " + StartStopActivityComputer.ActivityPathString(data.RelatedActivityID));
                }
            }

            if (taskName == null)
                taskName = data.TaskName;

            // Create the new activity.  
            StartStopActivity activity;
            if (activityId != null)
                activity = new StartStopActivity(data, taskName, ref *activityId, creator, m_nextIndex++, extraStartInfo);
            else
            {
                Guid activityIdValue = data.ActivityID;
                activity = new StartStopActivity(data, taskName, ref activityIdValue, creator, m_nextIndex++, extraStartInfo);
            }
            if (creator != null)
                creator.LiveChildCount++;
            SetActiveStartStopActivityTable(activity.ActivityID, data.ProcessID, activity);       // Put it in our table of live activities.  
            m_traceActivityToStartStopActivity.Set((int)taskIndex, activity);

            // Issue callback if requested AFTER state update
            var onStartAfter = Start;
            if (onStartAfter != null)
                onStartAfter(activity, data);
        }

        void SetThreadToStartStopActivity(TraceEvent data, Guid activityId)
        {
            StartStopActivity startStopActivity = GetActiveStartStopActivityTable(activityId, data.ProcessID);
            if (startStopActivity != null)
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

                StartStopActivity previousStartStopActivity = m_traceActivityToStartStopActivity.Get((int)taskIndex);
                if (previousStartStopActivity == null)
                    m_traceActivityToStartStopActivity.Set((int)taskIndex, startStopActivity);
                else if (previousStartStopActivity != startStopActivity)
                    Trace.WriteLine("Warning: Thread " + data.ThreadID + " at " + data.TimeStampRelativeMSec.ToString("n3") + 
                        " wants to overwrite activity started at " + previousStartStopActivity.StartTimeRelativeMSec.ToString("n3"));
            }
        }

        unsafe private void OnStop(TraceEvent data, Guid* activityID = null)
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
            if (activityID != null)
                activity = GetActiveStartStopActivityTable(*activityID, data.ProcessID);
            else
                activity = GetActiveStartStopActivityTable(data.ActivityID, data.ProcessID);
            if (activity != null)
            {
                // We defer the stop until the NEXT event (so that stops look like they are IN the activity). 
                // So here we just capture what is needed to do this later (basically on the next event).  
                activity.RememberStop(data.EventIndex, data.TimeStampRelativeMSec, taskIndex);
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
                {
                    Guid activityIdValue = activityID != null ? *activityID : data.ActivityID;
                    Trace.WriteLine("Warning: Unmatched stop at " + data.TimeStampRelativeMSec.ToString("n3") + " ID = " + activityIdValue);
                }
            }
        }

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
                RemoveActiveStartStopActitityTable(startStopActivity.ActivityID, startStopActivity.ProcessID);

                var creator = startStopActivity.Creator;
                if (creator != null)
                {
                    --creator.LiveChildCount;
                    Debug.Assert(0 <= creator.LiveChildCount);
                }

                startStopActivity.activityIndex = ActivityIndex.Invalid;    // This also marks the activity as truly stopped.  
                m_deferredStop = null;
            }
        }


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
                    OnStart(data, extraStartInfo, &startStopId, thread, creator, "SQLCommand");
                    return;
                }
                else
                {
                    Debug.Assert(data.ID == (TraceEventID)2); // EndExecute
                    OnStop(data, &startStopId);
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
                    OnStart(data, url, &startStopId, thread, creator, taskName);
                }
                else
                {
                    Debug.Assert(data.Opcode == TraceEventOpcode.Stop);
                    OnStop(data, &startStopId);
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

                // In V4.6 we use a ActivityPath but this is a bug in that the 'STOP will not necessarily match it. 
                // In V4.7 and beyond this is fixed (thus the ID will NOT be a ActivityPath).  
                // Thus this can be removed after V4.6 is aged out (e.g. 9/2016)
                // To fix V4.6 we set the ID 
                Guid unfixedActivityID = Guid.Empty;
                if (StartStopActivityComputer.IsActivityPath(activityID))
                {
                    unfixedActivityID = threadToLastAspNetGuid[(int)thread.ThreadIndex];
                    if (unfixedActivityID != Guid.Empty)
                    {
                        if (unfixedActivityID != activityID)
                        {
                            // Also add the old activity path to the extraStartInfo, which is needed ServiceProfiler versioning between the monitor and the detailed file.  
                            extraStartInfo = StartStopActivityComputer.ActivityPathString(activityID) + "," + extraStartInfo;
                            activityID = unfixedActivityID;       // Make the fixed key the 'offiical' one.
                        }
                        else
                            unfixedActivityID = Guid.Empty;
                    }
                    else
                        Trace.WriteLine(data.TimeStampRelativeMSec.ToString("n3") + " Could not find ASP.NET Send event to fix Start event");
                }

                OnStart(data, extraStartInfo, &activityID, thread, null, taskName);

                // in V4.6 the Stop may use the fixed or unfixed ID, so put both IDs into the lookup table.  
                // Note that this leaks table entries, but this 
                if (unfixedActivityID != Guid.Empty)
                    SetActiveStartStopActivityTable(unfixedActivityID, data.ProcessID, GetActiveStartStopActivityTable(activityID, data.ProcessID));
            }
            else
            {
                Debug.Assert(data.Opcode == TraceEventOpcode.Stop);
                OnStop(data);
            }
            threadToLastAspNetGuid[(int)thread.ThreadIndex] = Guid.Empty;
        }

        // Code for looking up start-stop events by their activity ID and process ID

        /// <summary>
        /// Look up a start-stop activity by its ID.   Note that the 'activityID' needs to be unique for that instance 
        /// within a process.  (across ALL start-stop activities, which means it may need components that encode its 
        /// provider and task).   We pass the process ID as well so that it will be unique in the whole trace.  
        /// </summary>
        unsafe StartStopActivity GetActiveStartStopActivityTable(Guid activityID, int processID)
        {
            StartStopActivity ret = null;
            long* asLongs = (long*)&activityID;
            asLongs[1] += processID;    // add in the process ID.       Note that this does not guarentee non-collision we may wish to do better.  
            m_activeStartStopActivities.TryGetValue(activityID, out ret);
            return ret;
        }
        unsafe void SetActiveStartStopActivityTable(Guid activityID, int processID, StartStopActivity newValue)
        {
            long* asLongs = (long*)&activityID;
            asLongs[1] += processID;    // add in the process ID.       Note that this does not guarentee non-collision we may wish to do better.  
            m_activeStartStopActivities[activityID] = newValue;
        }
        unsafe void RemoveActiveStartStopActitityTable(Guid activityID, int processID)
        {
            long* asLongs = (long*)&activityID;
            asLongs[1] += processID;    // add in the process ID.       Note that this does not guarentee non-collision we may wish to do better.  
            m_activeStartStopActivities.Remove(activityID);
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

        // Fields
        TraceLogEventSource m_source;                                                // Where we get events from.  
        ActivityComputer m_taskComputer;                                             // I need to be able to get the current Activity to keep track of start-stop activities. 
        GrowableArray<StartStopActivity> m_traceActivityToStartStopActivity;         // Maps a TraceActivity (index) to a start stop activity at the current time. 
        Dictionary<StartStopKey, StartStopActivity> m_activeStartStopActivities;     // Lookup activities by activityID&ProcessID (we call the start-stop key) at the current time
        int m_nextIndex;                                                             // Used to create unique indexes for StartStopActivity.Index.  
        StartStopActivity m_deferredStop;                                            // We defer doing the stop action until the next event.  This is what remembers to do this.  
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
        public bool IsStopped { get { return DurationMSec != 0 && activityIndex == ActivityIndex.Invalid; } }
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
        internal void RememberStop(EventIndex stopEventIndex, double stopTimeRelativeMSec, ActivityIndex activityIndex)
        {
            if (DurationMSec == 0)
            {
                this.activityIndex = activityIndex;
                this.StopEventIndex = stopEventIndex;
                this.DurationMSec = stopTimeRelativeMSec - StartTimeRelativeMSec;
            }
        }

        static int s_nextChildID;       // Used to generate small IDs 
        private int myChildID;
        private int nextChildID;

        // these are used to implement deferred stops.  
        internal ActivityIndex activityIndex;     // the index for the task that was active at the time of the stop.  
        #endregion
    };

}