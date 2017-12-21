using System;

namespace Microsoft.Diagnostics.Tracing.Stacks
{
    /// <summary>
    /// This structure provides a clean API for a lightweight recursion stack guard to prevent
    /// <see cref="StackOverflowException"/>.
    /// </summary>
    internal struct RecursionGuard
    {
        /// <summary>
        /// For recursive methods that need to process deep stacks, this constant defines the limit for recursion within
        /// a single thread. After reaching this limit, methods need to trampoline to a new thread before continuing to
        /// recurse.
        /// </summary>
        internal const int SingleThreadRecursionLimit = 400;

        private readonly int _currentThreadRecursionDepth;

        private RecursionGuard(int currentThreadRecursionDepth)
        {
            _currentThreadRecursionDepth = currentThreadRecursionDepth;
        }

        /// <summary>
        /// Gets the recursion guard for entering a recursive method.
        /// </summary>
        /// <remarks>
        /// This is equivalent to the default <see cref="RecursionGuard"/> value.
        /// </remarks>
        public static RecursionGuard Entry => default(RecursionGuard);

        /// <summary>
        /// Gets an updated recursion guard for recursing into a method.
        /// </summary>
        public RecursionGuard Recurse => new RecursionGuard(_currentThreadRecursionDepth + 1);

        /// <summary>
        /// Gets an updated recursion guard for continuing execution on a new thread.
        /// </summary>
        public RecursionGuard ResetOnNewThread => Entry;

        /// <summary>
        /// Gets a value indicating whether the current operation has exceeded the recursion depth for a single thread,
        /// and needs to continue executing on a new thread.
        /// </summary>
        public bool RequiresNewThread => _currentThreadRecursionDepth > SingleThreadRecursionLimit;
    }
}
