using System.Threading.Tasks;

namespace PerfViewTests.Utilities
{
    /// <summary>
    /// This type provides the type argument for a <see cref="TaskCompletionSource{TResult}"/> in cases where the
    /// resulting task is intended to represent a <see cref="Task"/> which is not a <see cref="Task{TResult}"/> (i.e.
    /// one which does not have a result.
    /// </summary>
    internal struct VoidResult
    {
    }
}
