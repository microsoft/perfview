using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class DesktopStackFrame : ClrStackFrame
    {
        public override Address StackPointer
        {
            get { return m_sp; }
        }

        public override Address InstructionPointer
        {
            get { return m_ip; }
        }

        public override ClrStackFrameType Kind
        {
            get { return m_type; }
        }

        public override string DisplayString
        {
            get { return m_frameName; }
        }

        public override ClrMethod Method
        {
            get
            {
                if (m_method == null && m_ip != 0 && m_type == ClrStackFrameType.ManagedMethod)
                    m_method = m_runtime.GetMethodByAddress(m_ip);

                return m_method;
            }
        }

        public override string ToString()
        {
            if (m_type == ClrStackFrameType.ManagedMethod)
                return m_frameName;

            int methodLen = 0;
            int methodTypeLen = 0;

            if (m_method != null)
            {
                methodLen = m_method.Name.Length;
                if (m_method.Type != null)
                    methodTypeLen = m_method.Type.Name.Length;
            }

            StringBuilder sb = new StringBuilder(m_frameName.Length + methodLen + methodTypeLen + 10);

            sb.Append('[');
            sb.Append(m_frameName);
            sb.Append(']');

            if (m_method != null)
            {
                sb.Append(" (");

                if (m_method.Type != null)
                {
                    sb.Append(m_method.Type.Name);
                    sb.Append('.');
                }

                sb.Append(m_method.Name);
                sb.Append(')');
            }

            return sb.ToString();
        }

        public override SourceLocation GetFileAndLineNumber()
        {
            if (m_type == ClrStackFrameType.Runtime)
                return null;

            ClrMethod method = Method;
            if (method == null)
                return null;

            return method.GetSourceLocationForOffset(m_ip - method.NativeCode);
        }

        public DesktopStackFrame(DesktopRuntimeBase runtime, ulong ip, ulong sp, string method)
        {
            m_ip = ip;
            m_sp = sp;
            m_frameName = method;
            m_type = ClrStackFrameType.ManagedMethod;
            m_runtime = runtime;
        }

        public DesktopStackFrame(DesktopRuntimeBase runtime, ulong sp, string method)
        {
            m_sp = sp;
            m_frameName = method;
            m_type = ClrStackFrameType.Runtime;
            m_runtime = runtime;
        }

        public DesktopStackFrame(DesktopRuntimeBase runtime, ulong sp, string method, ClrMethod innerMethod)
        {
            m_sp = sp;
            m_frameName = method;
            m_type = ClrStackFrameType.Runtime;
            m_method = innerMethod;
            m_runtime = runtime;
        }

        ulong m_ip, m_sp;
        string m_frameName;
        ClrStackFrameType m_type;
        private ClrMethod m_method;
        private DesktopRuntimeBase m_runtime;
    }

    class DesktopThread : ClrThread
    {
        public override Address Address
        {
            get { return m_address; }
        }

        public override bool IsFinalizer
        {
            get { return m_finalizer; }
        }

        public override ClrException CurrentException
        {
            get
            {
                ulong ex = m_exception;
                if (ex == 0)
                    return null;

                if (!m_runtime.ReadPointer(ex, out ex) || ex == 0)
                    return null;

                return m_runtime.GetHeap().GetExceptionObject(ex);
            }
        }

        public override bool IsGC
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_GC) == (int)TlsThreadType.ThreadType_GC; }
        }

        public override bool IsDebuggerHelper
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_DbgHelper) == (int)TlsThreadType.ThreadType_DbgHelper; }
        }

        public override bool IsThreadpoolTimer
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_Timer) == (int)TlsThreadType.ThreadType_Timer; }
        }

        public override bool IsThreadpoolCompletionPort
        {
            get
            {
                return (ThreadType & (int)TlsThreadType.ThreadType_Threadpool_IOCompletion) == (int)TlsThreadType.ThreadType_Threadpool_IOCompletion
                    || (m_threadState & (int)ThreadState.TS_CompletionPortThread) == (int)ThreadState.TS_CompletionPortThread;
            }
        }

        public override bool IsThreadpoolWorker
        {
            get
            {
                return (ThreadType & (int)TlsThreadType.ThreadType_Threadpool_Worker) == (int)TlsThreadType.ThreadType_Threadpool_Worker
                    || (m_threadState & (int)ThreadState.TS_TPWorkerThread) == (int)ThreadState.TS_TPWorkerThread;
            }
        }

        public override bool IsThreadpoolWait
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_Wait) == (int)TlsThreadType.ThreadType_Wait; }
        }

        public override bool IsThreadpoolGate
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_Gate) == (int)TlsThreadType.ThreadType_Gate; }
        }

        public override bool IsSuspendingEE
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_DynamicSuspendEE) == (int)TlsThreadType.ThreadType_DynamicSuspendEE; }
        }

        public override bool IsShutdownHelper
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_ShutdownHelper) == (int)TlsThreadType.ThreadType_ShutdownHelper; }
        }



        public override bool IsAborted
        {
            get { return (m_threadState & (int)ThreadState.TS_Aborted) == (int)ThreadState.TS_Aborted; }
        }

        public override bool IsGCSuspendPending
        {
            get { return (m_threadState & (int)ThreadState.TS_GCSuspendPending) == (int)ThreadState.TS_GCSuspendPending; }
        }

        public override bool IsUserSuspended
        {
            get { return (m_threadState & (int)ThreadState.TS_UserSuspendPending) == (int)ThreadState.TS_UserSuspendPending; }
        }

        public override bool IsDebugSuspended
        {
            get { return (m_threadState & (int)ThreadState.TS_DebugSuspendPending) == (int)ThreadState.TS_DebugSuspendPending; }
        }

        public override bool IsBackground
        {
            get { return (m_threadState & (int)ThreadState.TS_Background) == (int)ThreadState.TS_Background; }
        }

        public override bool IsUnstarted
        {
            get { return (m_threadState & (int)ThreadState.TS_Unstarted) == (int)ThreadState.TS_Unstarted; }
        }

        public override bool IsCoInitialized
        {
            get { return (m_threadState & (int)ThreadState.TS_CoInitialized) == (int)ThreadState.TS_CoInitialized; }
        }

        public override GcMode GcMode
        {
            get { return m_preemptive ? GcMode.Preemptive : GcMode.Cooperative; }
        }

        public override bool IsSTA
        {
            get { return (m_threadState & (int)ThreadState.TS_InSTA) == (int)ThreadState.TS_InSTA; }
        }

        public override bool IsMTA
        {
            get { return (m_threadState & (int)ThreadState.TS_InMTA) == (int)ThreadState.TS_InMTA; }
        }

        public override bool IsAbortRequested
        {
            get
            {
                return (m_threadState & (int)ThreadState.TS_AbortRequested) == (int)ThreadState.TS_AbortRequested
                    || (m_threadState & (int)ThreadState.TS_AbortInitiated) == (int)ThreadState.TS_AbortInitiated;
            }
        }
        

        public override bool IsAlive { get { return m_osThreadId != 0; } }
        public override uint OSThreadId { get { return m_osThreadId; } }
        public override int ManagedThreadId { get { return (int)m_managedThreadId; } }
        public override ulong AppDomain { get { return m_appDomain; } }
        public override uint LockCount { get { return m_lockCount; } }
        public override ulong Teb { get { return m_teb; } }
        public override ulong StackBase
        {
            get
            {
                if (m_teb == 0)
                    return 0;

                ulong ptr = m_teb + (ulong)IntPtr.Size;
                if (!m_runtime.ReadPointer(ptr, out ptr))
                    return 0;

                return ptr;
            }
        }

        public override ulong StackLimit
        {
            get
            {
                if (m_teb == 0)
                    return 0;

                ulong ptr = m_teb + (ulong)IntPtr.Size * 2;
                if (!m_runtime.ReadPointer(ptr, out ptr))
                    return 0;

                return ptr;
            }
        }

        public override IEnumerable<ClrRoot> EnumerateStackObjects()
        {
            return m_runtime.EnumerateStackReferences(this, true);
        }


        public override IEnumerable<ClrRoot> EnumerateStackObjects(bool includePossiblyDead)
        {
            return m_runtime.EnumerateStackReferences(this, includePossiblyDead);
        }

        public override IList<ClrStackFrame> StackTrace
        {
            get
            {
                if (m_stackTrace == null)
                    m_stackTrace = m_runtime.GetStackTrace(OSThreadId);

                return m_stackTrace;
            }
        }

        public override IList<BlockingObject> BlockingObjects
        {
            get
            {
                ((DesktopGCHeap)m_runtime.GetHeap()).InitLockInspection();

                if (m_blockingObjs == null)
                    return new BlockingObject[0];
                return m_blockingObjs;
            }
        }

        internal void SetBlockingObjects(BlockingObject[] blobjs)
        {
            m_blockingObjs = blobjs;
        }

        #region Helper Enums
        internal enum TlsThreadType
        {
            ThreadType_GC = 0x00000001,
            ThreadType_Timer = 0x00000002,
            ThreadType_Gate = 0x00000004,
            ThreadType_DbgHelper = 0x00000008,
            //ThreadType_Shutdown = 0x00000010,
            ThreadType_DynamicSuspendEE = 0x00000020,
            //ThreadType_Finalizer = 0x00000040,
            //ThreadType_ADUnloadHelper = 0x00000200,
            ThreadType_ShutdownHelper = 0x00000400,
            ThreadType_Threadpool_IOCompletion = 0x00000800,
            ThreadType_Threadpool_Worker = 0x00001000,
            ThreadType_Wait = 0x00002000,
        }

        enum ThreadState
        {
            //TS_Unknown                = 0x00000000,    // threads are initialized this way

            TS_AbortRequested         = 0x00000001,    // Abort the thread
            TS_GCSuspendPending       = 0x00000002,    // waiting to get to safe spot for GC
            TS_UserSuspendPending     = 0x00000004,    // user suspension at next opportunity
            TS_DebugSuspendPending    = 0x00000008,    // Is the debugger suspending threads?
            //TS_GCOnTransitions        = 0x00000010,    // Force a GC on stub transitions (GCStress only)

            //TS_LegalToJoin            = 0x00000020,    // Is it now legal to attempt a Join()
            //TS_YieldRequested         = 0x00000040,    // The task should yield
            //TS_Hijacked               = 0x00000080,    // Return address has been hijacked
            //TS_BlockGCForSO           = 0x00000100,    // If a thread does not have enough stack, WaitUntilGCComplete may fail.
                                                       // Either GC suspension will wait until the thread has cleared this bit,
                                                       // Or the current thread is going to spin if GC has suspended all threads.
            TS_Background             = 0x00000200,    // Thread is a background thread
            TS_Unstarted              = 0x00000400,    // Thread has never been started
            //TS_Dead                   = 0x00000800,    // Thread is dead

            //TS_WeOwn                  = 0x00001000,    // Exposed object initiated this thread
            TS_CoInitialized          = 0x00002000,    // CoInitialize has been called for this thread

            TS_InSTA                  = 0x00004000,    // Thread hosts an STA
            TS_InMTA                  = 0x00008000,    // Thread is part of the MTA

            // Some bits that only have meaning for reporting the state to clients.
            //TS_ReportDead             = 0x00010000,    // in WaitForOtherThreads()

            //TS_TaskReset              = 0x00040000,    // The task is reset

            //TS_SyncSuspended          = 0x00080000,    // Suspended via WaitSuspendEvent
            //TS_DebugWillSync          = 0x00100000,    // Debugger will wait for this thread to sync

            //TS_StackCrawlNeeded       = 0x00200000,    // A stackcrawl is needed on this thread, such as for thread abort
                                                       // See comment for s_pWaitForStackCrawlEvent for reason.

            //TS_SuspendUnstarted       = 0x00400000,    // latch a user suspension on an unstarted thread

            TS_Aborted                = 0x00800000,    // is the thread aborted?
            TS_TPWorkerThread         = 0x01000000,    // is this a threadpool worker thread?

            //TS_Interruptible          = 0x02000000,    // sitting in a Sleep(), Wait(), Join()
            //TS_Interrupted            = 0x04000000,    // was awakened by an interrupt APC. !!! This can be moved to TSNC

            TS_CompletionPortThread   = 0x08000000,    // Completion port thread

            TS_AbortInitiated         = 0x10000000,    // set when abort is begun

            //TS_Finalized              = 0x20000000,    // The associated managed Thread object has been finalized.
                                                       // We can clean up the unmanaged part now.

            //TS_FailStarted            = 0x40000000,    // The thread fails during startup.
            //TS_Detached               = 0x80000000,    // Thread was detached by DllMain
        }
        #endregion

        #region Internal Methods
        void InitTls()
        {
            if (m_tlsInit)
                return;

            m_tlsInit = true;
            
            m_threadType = GetTlsSlotForThread(m_runtime, Teb);
        }

        internal static int GetTlsSlotForThread(RuntimeBase runtime, ulong teb)
        {
            const int maxTlsSlot = 64;
            const int tlsSlotOffset = 0x1480; // Same on x86 and amd64
            const int tlsExpansionSlotsOffset = 0x1780;
            uint ptrSize = (uint)runtime.PointerSize;

            ulong lowerTlsSlots = teb + tlsSlotOffset;
            uint clrTlsSlot = runtime.GetTlsSlot();
            if (clrTlsSlot == uint.MaxValue)
                return 0;

            ulong tlsSlot = 0;
            if (clrTlsSlot < maxTlsSlot)
            {
                tlsSlot = lowerTlsSlots + ptrSize * clrTlsSlot;
            }
            else
            {
                if (!runtime.ReadPointer(teb + tlsExpansionSlotsOffset, out tlsSlot) || tlsSlot == 0)
                    return 0;

                tlsSlot += ptrSize * (clrTlsSlot - maxTlsSlot);
            }

            ulong clrTls = 0;
            if (!runtime.ReadPointer(tlsSlot, out clrTls))
                return 0;

            // Get thread data;

            uint tlsThreadTypeIndex = runtime.GetThreadTypeIndex();
            if (tlsThreadTypeIndex == uint.MaxValue)
                return 0;

            ulong threadType = 0;
            if (!runtime.ReadPointer(clrTls + ptrSize * tlsThreadTypeIndex, out threadType))
                return 0;

            return (int)threadType;
        }

        internal DesktopThread(RuntimeBase clr, IThreadData thread, ulong address, bool finalizer)
        {
            m_runtime = clr;
            m_address = address;
            m_finalizer = finalizer;

            Debug.Assert(thread != null);
            if (thread != null)
            {
                m_osThreadId = thread.OSThreadID;
                m_managedThreadId = thread.ManagedThreadID;
                m_appDomain = thread.AppDomain;
                m_lockCount = thread.LockCount;
                m_teb = thread.Teb;
                m_threadState = thread.State;
                m_exception = thread.ExceptionPtr;
                m_preemptive = thread.Preemptive;
            }

        }

        
        uint m_osThreadId;
        private RuntimeBase m_runtime;
        private IList<ClrStackFrame> m_stackTrace;
        private bool m_finalizer;

        private bool m_tlsInit;
        int m_threadType;
        int m_threadState;
        private uint m_managedThreadId;
        private uint m_lockCount;
        private ulong m_address;
        private ulong m_appDomain;
        private ulong m_teb;
        private ulong m_exception;
        private BlockingObject[] m_blockingObjs;
        private bool m_preemptive;
        private int ThreadType { get { InitTls(); return m_threadType; } }
        #endregion
    }

    class LocalVarRoot : ClrRoot
    {
        private bool m_pinned;
        private bool m_falsePos;
        private bool m_interior;
        private ClrThread m_thread;
        private ClrType m_type;

        public LocalVarRoot(ulong addr, ulong obj, ClrType type, ClrThread thread, bool pinned, bool falsePos, bool interior)
        {
            Address = addr;
            Object = obj;
            m_pinned = pinned;
            m_falsePos = falsePos;
            m_interior = interior;
            m_thread = thread;
            m_type = type;
        }

        public override ClrThread Thread
        {
            get
            {
                return m_thread;
            }
        }

        public override bool IsPossibleFalsePositive
        {
            get
            {
                return m_falsePos;
            }
        }

        public override string Name
        {
            get
            {
                return "local var";
            }
        }

        public override bool IsPinned
        {
            get
            {
                return m_pinned;
            }
        }

        public override GCRootKind Kind
        {
            get
            {
                return GCRootKind.LocalVar;
            }
        }

        public override bool IsInterior
        {
            get
            {
                return m_interior;
            }
        }

        public override ClrType Type
        {
            get { return m_type; }
        }
    }
}
