using Microsoft.Diagnostics.Tracing.Analysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public class AutomatedAnalysisTraceProcess
    {
        public AutomatedAnalysisTraceProcess()
        {

        }

        public AutomatedAnalysisTraceProcess(int uniqueID, int displayID, string description, bool containsManagedCode)
        {
            UniqueID = uniqueID;
            DisplayID = displayID;
            Description = description;
            ContainsManagedCode = containsManagedCode;
        }

        public int UniqueID { get; set; }

        public int DisplayID { get; set; }

        public string Description { get; set; }

        public virtual bool ContainsManagedCode { get; }

        public override int GetHashCode()
        {
            return UniqueID;
        }

        public override bool Equals(object obj)
        {
            if(obj is AutomatedAnalysisTraceProcess)
            {
                return UniqueID == ((AutomatedAnalysisTraceProcess)obj).UniqueID;
            }

            return false;
        }
    }
}
