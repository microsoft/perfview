// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Diagnostics.Tracing.Computers;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeSupport;

namespace TraceEventTests
{
    internal sealed class AsyncProfilerRejectRuntimeBridgeSupport : AsyncProfilerRuntimeSupport
    {
        public AsyncProfilerRejectRuntimeBridgeSupport() : base("Test-only rejected bridge layout")
        {
        }

        protected override AsyncProfilerRuntimeSequenceMatch MatchRuntimeResumptionBridge(
            AsyncProfilerCallStackTransformState context, int start, int end) =>
            end > start ? AsyncProfilerRuntimeSequenceMatch.Reject(end - start) : default;
    }

    // Hypothetical implementation layouts, never selected by production version detection.
    internal sealed class AsyncProfilerExtendedRuntimeLayoutSupport : AsyncProfilerRuntimeSupport
    {
        public AsyncProfilerExtendedRuntimeLayoutSupport() : base("Test-only extended runtime layouts")
        {
        }

        public const string HelperName = "System.Runtime.CompilerServices.AsyncProfiler.LayoutHelper";
        public const string AnchorName = "System.Threading.ThreadPoolWorkQueue.ChangedDispatch";

        public override FrameClassification ClassifySynchronousFrame(string frameName, bool isHostModule)
        {
            if (!isHostModule)
            {
                return base.ClassifySynchronousFrame(frameName, false);
            }
            if (frameName == HelperName)
            {
                return AsyncProfilerStitchSyncFrameKind.V1MethodHelper;
            }
            if (frameName == AnchorName)
            {
                return AsyncProfilerStitchSyncFrameKind.V1DispatchSetup;
            }
            return base.ClassifySynchronousFrame(frameName, true);
        }

        public override bool IsRuntimeResumptionAnchor(AsyncProfilerStitchedFrame frame) =>
            frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
            frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1DispatchSetup;

        public override bool IsOptionalInfrastructureFrame(AsyncProfilerStitchedFrame frame, AsyncProfilerCallStackStitchBoundaryKind boundary) =>
            !IsRuntimeResumptionAnchor(frame) && base.IsOptionalInfrastructureFrame(frame, boundary);

        protected override AsyncProfilerRuntimeSequenceMatch MatchV1Startup(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            AsyncProfilerRuntimeSequenceMatch common = base.MatchV1Startup(context, start, end);
            if (!common.IsMatch || end - start < 3 ||
                context.Frames[start + 1].SyncFrameKind != AsyncProfilerStitchSyncFrameKind.V1MethodHelper ||
                context.Frames[start + 2].SyncClassification.RuntimeKind != RuntimeFrameKind.V1Startup)
            {
                return common;
            }
            int next = start + 3;
            while (next < end && context.Frames[next].SyncClassification.RuntimeKind == RuntimeFrameKind.V1Startup)
            {
                next++;
            }
            return AsyncProfilerRuntimeSequenceMatch.Match(next - start);
        }

        protected override AsyncProfilerRuntimeSequenceMatch MatchV1AwaitRegistration(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            AsyncProfilerRuntimeSequenceMatch common = base.MatchV1AwaitRegistration(context, start, end);
            return common.IsMatch && start + common.Length < end &&
                   context.Frames[start + common.Length].SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1MethodHelper
                ? AsyncProfilerRuntimeSequenceMatch.Match(common.Length + 1) : common;
        }

        protected override AsyncProfilerRuntimeSequenceMatch MatchProfilerEntry(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            if (end - start >= 2 && end < context.Frames.Count &&
                context.Frames[start].SyncClassification.RuntimeKind == RuntimeFrameKind.ProfilerResume &&
                context.Frames[start + 1].SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1MethodHelper)
            {
                bool dispatcher = context.Frames[end].Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget &&
                    !context.Frames[end].IsStateMachineBoxCarrier;
                return end - start == (dispatcher ? 3 : 2) &&
                       (!dispatcher || context.ClassifyBoundary(context.Frames[start + 2].CodeAddress).Kind ==
                            AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher)
                    ? AsyncProfilerRuntimeSequenceMatch.Match(end - start) : default;
            }
            return base.MatchProfilerEntry(context, start, end);
        }
    }

    // A hypothetical layout, never selected by production version detection.
    internal sealed class AsyncProfilerExtendedCompletionRuntimeSupport : AsyncProfilerRuntimeSupport
    {
        public AsyncProfilerExtendedCompletionRuntimeSupport(
            Func<AsyncProfilerCallStackTransformState, AsyncProfilerStitchedFrame, string> methodName = null)
            : base("Test-only extended completion layout")
        {
            m_methodName = methodName ??
                ((context, frame) => context.TraceLog.CodeAddresses[frame.CodeAddress].FullMethodName);
        }

        public const string InjectedHelperName = "System.Threading.Tasks.Task.CompletionHelper";
        public const string RenamedCompletionName = "System.Threading.Tasks.Task.TryComplete";

        public override FrameClassification ClassifySynchronousFrame(string frameName, bool isHostModule) =>
            isHostModule && frameName == RenamedCompletionName
                ? (FrameClassification)RuntimeFrameKind.TaskResult
                : base.ClassifySynchronousFrame(frameName, isHostModule);

        protected override AsyncProfilerRuntimeSequenceMatch MatchTaskCompletion(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            if (start == end || !IsKind(context.Frames[start], RuntimeFrameKind.TaskContinuations))
            {
                return default;
            }
            return end - start >= 3 &&
                   context.Frames[start + 1].Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                   m_methodName(context, context.Frames[start + 1]) == InjectedHelperName &&
                   IsKind(context.Frames[start + 2], RuntimeFrameKind.TaskResult)
                ? AsyncProfilerRuntimeSequenceMatch.Match(3)
                : AsyncProfilerRuntimeSequenceMatch.Reject(Math.Min(3, end - start));
        }

        protected override AsyncProfilerRuntimeSequenceMatch MatchTaskCompletionChain(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            AsyncProfilerRuntimeSequenceMatch match = base.MatchTaskCompletionChain(context, start, end);
            return match.Length == 0 && start < end &&
                   IsKind(context.Frames[start], RuntimeFrameKind.TaskContinuations)
                ? AsyncProfilerRuntimeSequenceMatch.Reject(Math.Min(4, end - start))
                : match;
        }

        protected override AsyncProfilerRuntimeSequenceMatch MatchV2CompletionHandoff(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            AsyncProfilerRuntimeSequenceMatch match = base.MatchV2CompletionHandoff(context, start, end);
            return match.Length == 0 && end - start >= 2 &&
                   IsKind(context.Frames[start], AsyncProfilerStitchSyncFrameKind.ContinuationBridge) &&
                   IsKind(context.Frames[start + 1], RuntimeFrameKind.TaskContinuations)
                ? AsyncProfilerRuntimeSequenceMatch.Reject(Math.Min(6, end - start))
                : match;
        }

        #region private

        private static bool IsKind(AsyncProfilerStitchedFrame frame, FrameClassification kind) =>
            frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync && frame.SyncClassification == kind;

        private readonly Func<AsyncProfilerCallStackTransformState, AsyncProfilerStitchedFrame, string> m_methodName;

        #endregion
    }
}
