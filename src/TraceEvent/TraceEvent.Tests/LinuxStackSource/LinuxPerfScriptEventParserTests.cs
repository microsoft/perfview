using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.StackSources;
using Xunit;

namespace TraceEventTests
{
    public class LinuxPerfScriptEventParserTests
    {
        /// <summary>
        /// A truncated sched_switch line whose expected next field name (e.g. "prev_pid") never
        /// appears used to send ReadProcessNameUntilNextField into an infinite loop, hanging the
        /// parser. This test ensures parsing such malformed input terminates instead of hanging.
        /// </summary>
        [Fact]
        public async Task TruncatedSchedSwitchDoesNotHang()
        {
            // Header parses far enough to reach ReadScheduleSwitch, then the input ends right after
            // "prev_comm=swapper" so the "prev_pid" field is never found.
            const string input = "myproc 100/100 [000] 1234.500000: sched:sched_switch: prev_comm=swapper";

            byte[] bytes = Encoding.ASCII.GetBytes(input);

            Task parseTask = Task.Run(() =>
            {
                LinuxPerfScriptEventParser parser = new LinuxPerfScriptEventParser();
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    foreach (var _ in parser.ParseSkippingPreamble(stream))
                    {
                    }
                }
            });

            // Before the fix this loops forever; allow generous headroom for slow CI machines.
            Task completedTask = await Task.WhenAny(parseTask, Task.Delay(30000));
            Assert.True(completedTask == parseTask, "Parsing a truncated sched_switch line did not terminate (infinite loop).");

            // Surface any exception thrown by the parse task.
            await parseTask;
        }
    }
}
