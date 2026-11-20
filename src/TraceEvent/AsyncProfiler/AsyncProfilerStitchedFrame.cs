// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeSupport;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>How a stitched frame relates to the sampled physical stack and captured async call stacks.</summary>
    internal enum AsyncProfilerStitchedFrameOrigin : byte
    {
        /// <summary>A frame retained from the sampled physical stack without a matched logical async frame.</summary>
        Sync = 0,

        /// <summary>
        /// An async method body matched on the sampled physical stack.
        /// Retains the sampled code address alongside the captured async identity.
        /// </summary>
        AsyncCurrent,

        /// <summary>
        /// An async frame taken from a captured call stack rather than matched on the physical stack.
        /// In stitched output, it provides logical caller ancestry without claiming current execution.
        /// </summary>
        AsyncCaller,

        /// <summary>
        /// A logical frame from the child's historical parent call stack, linked through dispatcher creation.
        /// This relationship does not establish that the parent is executing at sample time.
        /// </summary>
        AsyncContextParent,

        /// <summary>
        /// An async resumption target identified from dispatcher or entry evidence.
        /// No separate method body was matched; the sample may still be in runtime dispatch code.
        /// </summary>
        AsyncDispatchTarget,

        /// <summary>
        /// A V1 method body still on the sampled physical stack after its async operation completed.
        /// The invocation is still finishing completion work.
        /// </summary>
        V1CompletedBody,

        /// <summary>
        /// A marker for missing physical callers at the root of a reconstructed async stack.
        /// Rendered as <c>BROKEN</c> without inventing a caller or execution host.
        /// </summary>
        IncompletePhysicalRoot,

        /// <summary>
        /// Verified return work represented by the completed V1 call stack's logical root.
        /// The root body need not be present on the sampled physical stack.
        /// </summary>
        V1CompletionReturn,

        /// <summary>
        /// The historical parent frame inferred as current when the child was created, not at sample time.
        /// Its position is inferred from completion records, not from sampled parent execution.
        /// </summary>
        AsyncContextParentCurrent,

        /// <summary>A display label without physical or captured async-method identity.</summary>
        Synthetic,
    }

    /// <summary>The async-context membership of a <see cref="AsyncProfilerStitchedFrame"/>.</summary>
    internal enum AsyncProfilerStitchedFrameContextKind
    {
        /// <summary>The frame does not belong to an async context.</summary>
        None = 0,

        /// <summary>The frame belongs to an async context active on the sampled execution stack.</summary>
        Active,

        /// <summary>The frame belongs to a historical creation-parent context.</summary>
        ParentContext,
    }

    /// <summary>How a <see cref="AsyncProfilerStitchedFrame"/> should be presented when it is interned.</summary>
    internal enum AsyncProfilerStitchedFramePresentation : byte
    {
        /// <summary>Use the frame's native symbol without presentation changes.</summary>
        Native = 0,

        /// <summary>
        /// Present a compiler-generated V1 state-machine <c>MoveNext</c> frame as its logical source async method.
        /// The original code address remains attached to the frame for source and identity correlation.
        /// </summary>
        LogicalStateMachineMethod,

        /// <summary>
        /// Represent a contiguous span of recognized V1/V2 runtime machinery as
        /// <c>[Async dispatcher frames]</c>, retaining the span's leafmost physical frame identity.
        /// </summary>
        AsyncDispatcherFrames,
    }

    /// <summary>
    /// Shared roles used by stitching and presentation. Runtime support implementations recognize the implementing methods
    /// and keep any finer matching details separate. These roles are not dispatch boundaries.
    /// </summary>
    internal enum AsyncProfilerStitchSyncFrameKind : byte
    {
        /// <summary>No shared async role; a runtime support may still recognize this frame.</summary>
        None = 0,

        /// <summary>The physical body of a compiler-generated V1 state machine.</summary>
        V1StateMachineBody,

        /// <summary>A generated stub preparing to enter a V2 async method.</summary>
        V2ResumeStub,

        /// <summary>A runtime helper managing a V1 method, without proving that method completed.</summary>
        V1MethodHelper,

        /// <summary>An awaiter helper, removable only within a separately verified runtime span.</summary>
        AwaiterHelper,

        /// <summary>A helper transferring execution between continuations or execution contexts.</summary>
        ContinuationBridge,

        /// <summary>A fault-recording helper, not evidence that the current async method completed.</summary>
        FaultHandling,

        /// <summary>A helper completing the current V1 method's own result, not a nested operation's result.</summary>
        V1MethodCompletion,

        /// <summary>Profiler capture, emission, or resumption work that can identify an entry candidate.</summary>
        ProfilerEntry,

        /// <summary>Other profiler, diagnostics, event-source, or box support work.</summary>
        ProfilerHelper,

        /// <summary>Setup for a V1 dispatcher before await registration.</summary>
        V1DispatchSetup,

        /// <summary>A V1 box callback carrying method identity, but not a resumption carrier.</summary>
        V1BoxCallback,
    }

    /// <summary>
    /// One frame of a native (CPU-sample) call stack, as consumed by <see cref="AsyncProfilerCallStackStitcher"/>.
    /// Carries the sampled code address, canonical method identity, and optional runtime classification.
    /// </summary>
    internal readonly struct AsyncProfilerStitchSyncFrame
    {
        public AsyncProfilerStitchSyncFrame(CodeAddressIndex codeAddress, MethodIndex method)
            : this(codeAddress, method, AsyncProfilerStitchSyncFrameKind.None)
        {
        }

        public AsyncProfilerStitchSyncFrame(CodeAddressIndex codeAddress, MethodIndex method,
            FrameClassification classification)
            : this(codeAddress, method, classification, MethodIndex.Invalid)
        {
        }

        /// <summary>Creates a native frame with an optional uniquely resolved boxed state-machine method.</summary>
        public AsyncProfilerStitchSyncFrame(CodeAddressIndex codeAddress, MethodIndex method,
            FrameClassification classification, MethodIndex boxedStateMachineMethod)
        {
            CodeAddress = codeAddress;
            Method = method;
            Classification = classification;
            BoxedStateMachineMethod = boxedStateMachineMethod;
        }

        /// <summary>The frame's code address (used to intern it and to classify it as an async boundary).</summary>
        public readonly CodeAddressIndex CodeAddress;

        /// <summary>
        /// The canonical method used for V1/V2 identity matching, or <see cref="MethodIndex.Invalid"/>.
        /// </summary>
        public readonly MethodIndex Method;

        /// <summary>The frame's shared async role.</summary>
        public AsyncProfilerStitchSyncFrameKind Kind => Classification.Kind;

        /// <summary>The stitching role and runtime-helper classification of this physical frame.</summary>
        public readonly FrameClassification Classification;

        /// <summary>
        /// The uniquely resolved state-machine method carried by a CoreLib box execution frame, or
        /// <see cref="MethodIndex.Invalid"/>. Only the leafmost execution carrier preceding any physical
        /// async application body can use this identity for V1 alignment.
        /// </summary>
        public readonly MethodIndex BoxedStateMachineMethod;
    }

    /// <summary>
    /// A physical frame, logical async frame, display label, or missing-root marker in a stitched call stack.
    /// </summary>
    internal readonly struct AsyncProfilerStitchedFrame : IEquatable<AsyncProfilerStitchedFrame>
    {
        /// <summary>Preserves a physical frame and its runtime classification.</summary>
        public static AsyncProfilerStitchedFrame CreateSync(AsyncProfilerStitchSyncFrame frame, AsyncProfilerCallStack context) =>
            new AsyncProfilerStitchedFrame(
                AsyncProfilerStitchedFrameOrigin.Sync,
                frame.CodeAddress,
                frame.Method,
                null,
                -1,
                frame.Classification,
                AsyncProfilerStitchedFramePresentation.Native,
                context,
                frame.BoxedStateMachineMethod);

        /// <summary>
        /// Displays a physical runtime frame as <c>[Async dispatcher frames]</c>, retaining its identity.
        /// </summary>
        public static AsyncProfilerStitchedFrame CreateAsyncDispatcherFrames(AsyncProfilerStitchSyncFrame frame,
            AsyncProfilerCallStack context) =>
            CreateSync(frame, context).WithPresentation(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames);

        /// <summary>
        /// Creates a physically matched current async frame. With a box identity, creates a resumption target instead.
        /// In that case, the supplied address and method identify the physical helper, not the application body.
        /// </summary>
        public static AsyncProfilerStitchedFrame CreateAsyncCurrent(CodeAddressIndex codeAddress, MethodIndex method,
            AsyncProfilerCallStack context, int segmentFrameIndex, MethodIndex boxedStateMachineMethod = MethodIndex.Invalid) =>
            new AsyncProfilerStitchedFrame(
                boxedStateMachineMethod == MethodIndex.Invalid
                    ? AsyncProfilerStitchedFrameOrigin.AsyncCurrent
                    : AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget,
                codeAddress,
                method,
                context.Frames,
                segmentFrameIndex,
                AsyncProfilerStitchSyncFrameKind.None,
                GetAsyncPresentation(context),
                context,
                boxedStateMachineMethod);

        /// <summary>Preserves a sampled V1 method body after its async operation has completed.</summary>
        public static AsyncProfilerStitchedFrame CreateV1CompletedBody(CodeAddressIndex codeAddress, MethodIndex method, AsyncProfilerCallStack context, int segmentFrameIndex) =>
            new AsyncProfilerStitchedFrame(
                AsyncProfilerStitchedFrameOrigin.V1CompletedBody,
                codeAddress,
                method,
                context.Frames,
                segmentFrameIndex,
                AsyncProfilerStitchSyncFrameKind.None,
                AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod,
                context);

        /// <summary>
        /// Attributes verified return work to the completed V1 root method.
        /// The method body need not be physically present.
        /// </summary>
        public static AsyncProfilerStitchedFrame CreateV1CompletionReturn(AsyncProfilerCallStack context, bool isDispatcherTailUnwind = false) =>
            new AsyncProfilerStitchedFrame(
                AsyncProfilerStitchedFrameOrigin.V1CompletionReturn,
                context.Frames.CodeAddressAt(context.Frames.FrameCount - 1),
                MethodIndex.Invalid,
                context.Frames,
                context.Frames.FrameCount - 1,
                AsyncProfilerStitchSyncFrameKind.None,
                AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod,
                context,
                isDispatcherTailUnwind: isDispatcherTailUnwind);

        /// <summary>Creates a logical frame from captured async data.</summary>
        public static AsyncProfilerStitchedFrame CreateAsync(AsyncProfilerCallStack context, int segmentFrameIndex) =>
            new AsyncProfilerStitchedFrame(
                AsyncProfilerStitchedFrameOrigin.AsyncCaller,
                context.Frames.CodeAddressAt(segmentFrameIndex),
                MethodIndex.Invalid,
                context.Frames,
                segmentFrameIndex,
                AsyncProfilerStitchSyncFrameKind.None,
                GetAsyncPresentation(context), context);

        /// <summary>
        /// Creates a historical parent frame linked through child creation.
        /// <paramref name="isCurrent"/> marks the parent frame inferred as current at child creation,
        /// not at sample time.
        /// </summary>
        public static AsyncProfilerStitchedFrame CreateAsyncContextParent(AsyncProfilerCallStack context, int segmentFrameIndex, bool isCurrent = false) =>
            new AsyncProfilerStitchedFrame(
                isCurrent ? AsyncProfilerStitchedFrameOrigin.AsyncContextParentCurrent : AsyncProfilerStitchedFrameOrigin.AsyncContextParent,
                context.Frames.CodeAddressAt(segmentFrameIndex),
                MethodIndex.Invalid,
                context.Frames,
                segmentFrameIndex,
                AsyncProfilerStitchSyncFrameKind.None,
                GetAsyncPresentation(context),
                context);

        /// <summary>Creates a logical resumption target without a physically matched method body.</summary>
        public static AsyncProfilerStitchedFrame CreateAsyncDispatchTarget(AsyncProfilerCallStack context, int segmentFrameIndex) =>
            new AsyncProfilerStitchedFrame(
                AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget,
                context.Frames.CodeAddressAt(segmentFrameIndex),
                MethodIndex.Invalid,
                context.Frames,
                segmentFrameIndex,
                AsyncProfilerStitchSyncFrameKind.None,
                GetAsyncPresentation(context),
                context);

        /// <summary>
        /// Identifies a V1 resumption target through a physical box helper, without claiming body execution.
        /// </summary>
        public static AsyncProfilerStitchedFrame CreateAsyncDispatchTarget(AsyncProfilerStitchSyncFrame carrier, AsyncProfilerCallStack context, int segmentFrameIndex) =>
            new AsyncProfilerStitchedFrame(
                AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget,
                carrier.CodeAddress,
                carrier.Method,
                context.Frames,
                segmentFrameIndex,
                AsyncProfilerStitchSyncFrameKind.None,
                AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod,
                context,
                carrier.BoxedStateMachineMethod);

        /// <summary>Marks missing physical callers without inventing a caller or execution host.</summary>
        public static AsyncProfilerStitchedFrame CreateIncompletePhysicalRoot() =>
            new AsyncProfilerStitchedFrame(
                AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot,
                CodeAddressIndex.Invalid,
                MethodIndex.Invalid,
                null,
                -1,
                AsyncProfilerStitchSyncFrameKind.None,
                AsyncProfilerStitchedFramePresentation.Native,
                null);

        /// <summary>Creates a display label, optionally associated with an async call stack.</summary>
        internal static AsyncProfilerStitchedFrame CreateSynthetic(string displayName, AsyncProfilerCallStack context = null) =>
            new AsyncProfilerStitchedFrame(displayName, context);

        /// <summary>How this frame relates to physical and captured async frames.</summary>
        public readonly AsyncProfilerStitchedFrameOrigin Origin;

        /// <summary>
        /// The code address, or <see cref="CodeAddressIndex.Invalid"/> when unavailable.
        /// A valid address may still have unresolved symbols.
        /// </summary>
        public readonly CodeAddressIndex CodeAddress;

        /// <summary>
        /// The canonical physical method identity, or <see cref="MethodIndex.Invalid"/> when unavailable.
        /// </summary>
        public readonly MethodIndex Method;

        /// <summary>
        /// The canonical application method identified by a V1 box helper, or <see cref="MethodIndex.Invalid"/>.
        /// <see cref="Method"/> and <see cref="CodeAddress"/> identify the physical helper, not the application method.
        /// </summary>
        public readonly MethodIndex BoxedStateMachineMethod;

        /// <summary>
        /// True when a V1 resumption target was identified through a physical box helper.
        /// This does not mean the application body was sampled.
        /// </summary>
        public bool IsStateMachineBoxCarrier =>
            BoxedStateMachineMethod != MethodIndex.Invalid &&
            (Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent || Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);

        /// <summary>
        /// Captured async frames, or null when this frame has no captured async identity.
        /// Captured identity can be available even when <see cref="CodeAddress"/> is invalid.
        /// </summary>
        public AsyncProfilerCallStackFrames Segment => m_segmentOrDisplayName as AsyncProfilerCallStackFrames;

        /// <summary>
        /// The captured frame's index within <see cref="Segment"/>, or -1 when there is no captured frame.
        /// </summary>
        public readonly int SegmentFrameIndex;

        /// <summary>For a sync-origin frame, its shared async role.</summary>
        public readonly AsyncProfilerStitchSyncFrameKind SyncFrameKind;

        /// <summary>The physical frame's classification, retained for runtime matching and presentation.</summary>
        public FrameClassification SyncClassification => FrameClassification.FromRuntimeData(SyncFrameKind, m_syncRuntimeData);

        /// <summary>How this frame should be displayed.</summary>
        public readonly AsyncProfilerStitchedFramePresentation Presentation;

        /// <summary>The associated async call stack, or null.</summary>
        public readonly AsyncProfilerCallStack Context;

        /// <summary>The synthetic display label, or null for other frames.</summary>
        internal string DisplayName => m_segmentOrDisplayName as string;

        /// <summary>Whether this frame belongs to an active context, a historical parent context, or neither.</summary>
        public AsyncProfilerStitchedFrameContextKind ContextKind
        {
            get
            {
                if (Context == null)
                {
                    return AsyncProfilerStitchedFrameContextKind.None;
                }

                if (Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParent || Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParentCurrent)
                {
                    return AsyncProfilerStitchedFrameContextKind.ParentContext;
                }

                return AsyncProfilerStitchedFrameContextKind.Active;
            }
        }

        /// <summary>
        /// True when V1 return work was verified from a remaining dispatcher box, without a matched method body.
        /// </summary>
        internal bool IsDispatcherTailUnwind => m_isDispatcherTailUnwind;

        /// <summary>Returns a copy with a different presentation, keeping all other metadata.</summary>
        public AsyncProfilerStitchedFrame WithPresentation(AsyncProfilerStitchedFramePresentation presentation) =>
            new AsyncProfilerStitchedFrame(this, presentation);

        public bool Equals(AsyncProfilerStitchedFrame other) =>
            Origin == other.Origin &&
            CodeAddress == other.CodeAddress &&
            Method == other.Method &&
            BoxedStateMachineMethod == other.BoxedStateMachineMethod &&
            SegmentFrameIndex == other.SegmentFrameIndex &&
            SyncClassification.Equals(other.SyncClassification) &&
            Presentation == other.Presentation &&
            m_isDispatcherTailUnwind == other.m_isDispatcherTailUnwind &&
            ReferenceEquals(Context, other.Context) &&
            object.Equals(m_segmentOrDisplayName, other.m_segmentOrDisplayName);

        public override bool Equals(object obj) =>
            obj is AsyncProfilerStitchedFrame other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Origin;
                hash = (hash * 397) ^ (int)CodeAddress;
                hash = (hash * 397) ^ (int)Method;
                hash = (hash * 397) ^ (int)BoxedStateMachineMethod;
                hash = (hash * 397) ^ SegmentFrameIndex;
                hash = (hash * 397) ^ SyncClassification.GetHashCode();
                hash = (hash * 397) ^ (int)Presentation;
                hash = (hash * 397) ^ m_isDispatcherTailUnwind.GetHashCode();
                hash = (hash * 397) ^ (Context?.GetHashCode() ?? 0);
                return (hash * 397) ^ (m_segmentOrDisplayName?.GetHashCode() ?? 0);
            }
        }

        #region private

        private AsyncProfilerStitchedFrame(AsyncProfilerStitchedFrameOrigin origin,
            CodeAddressIndex codeAddress, MethodIndex method,
            AsyncProfilerCallStackFrames segment, int segmentFrameIndex, FrameClassification syncClassification,
            AsyncProfilerStitchedFramePresentation presentation, AsyncProfilerCallStack context,
            MethodIndex boxedStateMachineMethod = MethodIndex.Invalid, bool isDispatcherTailUnwind = false)
        {
            Origin = origin;
            CodeAddress = codeAddress;
            Method = method;
            BoxedStateMachineMethod = boxedStateMachineMethod;
            m_segmentOrDisplayName = segment;
            SegmentFrameIndex = segmentFrameIndex;
            SyncFrameKind = syncClassification.Kind;
            m_syncRuntimeData = syncClassification.RuntimeData;
            Presentation = presentation;
            Context = context;
            m_isDispatcherTailUnwind = isDispatcherTailUnwind;
        }

        private AsyncProfilerStitchedFrame(string displayName, AsyncProfilerCallStack context)
            : this(AsyncProfilerStitchedFrameOrigin.Synthetic, CodeAddressIndex.Invalid, MethodIndex.Invalid, null, -1,
                AsyncProfilerStitchSyncFrameKind.None, AsyncProfilerStitchedFramePresentation.Native, context)
        {
            m_segmentOrDisplayName = displayName;
        }

        private AsyncProfilerStitchedFrame(AsyncProfilerStitchedFrame frame,
            AsyncProfilerStitchedFramePresentation presentation)
        {
            this = frame;
            Presentation = presentation;
        }

        private static AsyncProfilerStitchedFramePresentation GetAsyncPresentation(AsyncProfilerCallStack context) =>
            context.Frames.Kind == AsyncCallstackKind.StateMachineAsync
                ? AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod
                : AsyncProfilerStitchedFramePresentation.Native;

        // Synthetic labels reuse the segment reference slot to keep ordinary stitched frames compact.
        private readonly object m_segmentOrDisplayName;
        private readonly bool m_isDispatcherTailUnwind;

        // Keep runtime data opaque. Separate byte fields avoid .NET Framework's padding
        // for an embedded classification struct.
        private readonly byte m_syncRuntimeData;

        #endregion
    }
}
