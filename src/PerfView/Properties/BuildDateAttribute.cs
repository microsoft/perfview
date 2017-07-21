using System;

namespace PerfView.Properties
{
    [AttributeUsage(AttributeTargets.Assembly)]
    internal sealed class BuildDateAttribute : Attribute
    {
        public BuildDateAttribute(string buildDate)
        {
            BuildDate = buildDate;
        }

        public string BuildDate
        {
            get;
        }
    }
}
