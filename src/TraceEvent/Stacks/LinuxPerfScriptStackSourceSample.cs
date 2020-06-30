using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tracing.Stacks
{
    public class LinuxPerfScriptStackSourceSample : StackSourceSample
    {
        /// <summary>
        ///  The CpuNumber the sample occurred on
        /// </summary>
        public int CpuNumber { get; set; }

        public LinuxPerfScriptStackSourceSample(StackSource source) : base(source)
        {

        }

        /// <summary>
        /// Copy a LinuxPerfScriptStackSourceSample from 'template'
        /// </summary>
        /// <param name="template"></param>
        public LinuxPerfScriptStackSourceSample(StackSourceSample template, int cpuNumber) : base(template)
        {
            CpuNumber = cpuNumber;
        }

        /// <summary>
        /// Copy a LinuxPerfScriptStackSourceSample from 'template'
        /// </summary>
        /// <param name="template"></param>
        public LinuxPerfScriptStackSourceSample(LinuxPerfScriptStackSourceSample template) : base(template)
        {
            CpuNumber = template.CpuNumber;
        }

        /// <summary>
        /// Gets a LinuxPerfScriptStackSourceSample
        /// </summary>

        #region overrides

        public override string ToString()
        {
            return String.Format("<Sample Metric=\"{0:f1}\" TimeRelativeMSec=\"{1:f3}\" StackIndex=\"{2}\" SampleIndex=\"{3}\" CpuNumber=\"{4}\">",
                Metric, TimeRelativeMSec, StackIndex, SampleIndex, CpuNumber);
        }
        #endregion
    }
}
