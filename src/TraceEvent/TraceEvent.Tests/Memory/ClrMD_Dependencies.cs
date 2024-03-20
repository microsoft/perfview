using Microsoft.Diagnostics.Runtime;
using Xunit;

namespace TraceEventTests
{
    public class ClrMD_Dependencies
    {
        /// <summary>
        /// If this test fails, it means that the ClrRootKind enum has changed.
        /// We should document this change in release notes, and possibly update
        /// GCHeapDumper.DumpRoots to keep the set of root names the same (unless a new root type is being added).
        /// </summary>
        [Fact]
        public void DetectClrRootKindChanges()
        {
            foreach (var kind in (ClrRootKind[])System.Enum.GetValues(typeof(ClrRootKind)))
            {
                switch(kind)
                {
                    case ClrRootKind.None:
                    case ClrRootKind.FinalizerQueue:
                    case ClrRootKind.StrongHandle:
                    case ClrRootKind.PinnedHandle:
                    case ClrRootKind.Stack:
                    case ClrRootKind.RefCountedHandle:
                    case ClrRootKind.AsyncPinnedHandle:
                    case ClrRootKind.SizedRefHandle:
                        break;
                    default:
                        Assert.True(false, $"Unexpected ClrRootKind: {kind}");
                        break;
                }
            }
        }
    }
}