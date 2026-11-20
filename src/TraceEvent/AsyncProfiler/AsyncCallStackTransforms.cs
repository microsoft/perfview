// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Configures normalization, infrastructure cleanup, annotations, and historical parent contexts for
    /// reconstructed async call stacks.
    /// Configure before stack generation. These settings affect presentation, not structural reconstruction.
    /// </summary>
    /// <remarks>
    /// Obtain this object from AsyncProfilerCallStackSourceBuilder.Transforms or a stack computer's
    /// AsyncCallStackTransforms property; it has no public constructor. Settings and callbacks are not
    /// thread-safe and must not be modified during stack production.
    /// </remarks>
    public sealed class AsyncCallStackTransforms
    {
        /// <summary>
        /// Enables conservative normalization of verified startup, reentrant, completion-return, and handoff
        /// patterns. Enabled by default. Disabling this preserves structural async reconstruction.
        /// </summary>
        public bool EnableNormalization
        {
            get => m_pipeline.EnableNormalization;
            set => m_pipeline.EnableNormalization = value;
        }

        /// <summary>
        /// Enables additional presentation cleanup and overhead attribution for verified async runtime
        /// infrastructure. Disabled by default. This is not general framework filtering and is independent
        /// of <see cref="EnableNormalization"/>.
        /// </summary>
        public bool EnableInfrastructureCleanup
        {
            get => m_pipeline.EnableInfrastructureCleanup;
            set => m_pipeline.EnableInfrastructureCleanup = value;
        }

        /// <summary>
        /// Labels active reconstructed async frame names with their V1/V2 kind and, where available, V1 presentation
        /// state. Disabled by default. Applied when frame names are emitted, without enabling additional
        /// reconstruction or cleanup. Historical parent contexts retain their context markers and V1/V2
        /// annotations independently of this setting.
        /// Verified current V1 frames display inferred state -1; verified completing/return-unwind frames
        /// display -2. Other V1 frames retain captured state, which is also preserved in CapturedState.
        /// </summary>
        public bool EnableFrameAnnotations
        {
            get => m_pipeline.EnableFrameAnnotations;
            set => m_pipeline.EnableFrameAnnotations = value;
        }

        /// <summary>
        /// Includes recorded historical creation-parent contexts in reconstructed async call stacks. Disabled
        /// by default. Requires captured creation relationships and parent resume intervals; unavailable parents
        /// are not inferred. If both normal-completion and exception-unwind records were observed for the parent's
        /// process, instrumentation kind, and configuration epoch, completed frames are removed at child creation
        /// time and the first remaining V1 frame displays inferred running state -1. Otherwise the captured
        /// resume/append stack is replayed unchanged. Captured states and IPs remain unchanged in frame metadata;
        /// this does not establish an exact creation-time execution IP or prove every progress record was captured.
        /// Existing stack computers still require EnableAsyncCallStacks separately.
        /// </summary>
        public bool EnableParentContexts
        {
            get => m_pipeline.EnableParentContexts;
            set => m_pipeline.EnableParentContexts = value;
        }

        /// <summary>
        /// The maximum number of historical parent contexts included per active async context when
        /// <see cref="EnableParentContexts"/> is enabled. Defaults to one. Does not limit frames within a
        /// context or simultaneously active nested contexts. Configure before stack generation.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">The value is not positive.</exception>
        public int MaximumParentContextDepth
        {
            get => m_pipeline.MaximumParentContextDepth;
            set => m_pipeline.MaximumParentContextDepth = value;
        }

        /// <summary>
        /// Adds a custom presentation callback after enabled built-in transforms, in registration order.
        /// Callback views are reused and must not be retained. Configure before producing stacks.
        /// Callbacks run only with usable async context; missing or empty segments use physical-only fallback.
        /// Parent-context expansion precedes callbacks; output interning, frame-name annotations, generated
        /// context-transition markers, and grouping roots follow them. Callback exceptions propagate to the caller.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="transform"/> is null.</exception>
        public void AddCustomTransform(Action<AsyncProfilerCallStackTransformContext> transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            var context = new AsyncProfilerCallStackTransformContext(transform);
            m_pipeline.AddCustomTransform(context.Invoke, requiresCompletionObservations: true);
        }

        /// <summary>Removes custom callbacks without changing built-in settings.</summary>
        public void ClearCustomTransforms() => m_pipeline.ClearCustomTransforms();

        #region private

        internal AsyncCallStackTransforms(AsyncProfilerCallStackTransformPipeline pipeline)
        {
            m_pipeline = pipeline;
        }

        private readonly AsyncProfilerCallStackTransformPipeline m_pipeline;

        #endregion
    }
}
