using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// Optional configuration information for an Analyzer.
    /// </summary>
    public sealed class AnalyzerConfiguration
    {
        private Dictionary<string, string> _properties = new Dictionary<string, string>();

        internal AnalyzerConfiguration()
        {
        }

        /// <summary>
        /// Add a key value pair.
        /// </summary>
        /// <param name="key">The unique key.</param>
        /// <param name="value">The value.</param>
        public void Add(string key, string value)
        {
            _properties.Add(key, value);
        }

        /// <summary>
        /// Attempt to fetch a configuration value using its unique key.
        /// </summary>
        /// <param name="key">The unique key.</param>
        /// <param name="value">The value.</param>
        /// <returns>True iff a configuration entry with the specified unique key was found.</returns>
        public bool TryGetValue(string key, out string value)
        {
            return _properties.TryGetValue(key, out value);
        }


        /// <summary>
        /// Attempt to fetch a configuration value using its unique key and convert it to a double.
        /// </summary>
        /// <param name="key">The unique key.</param>
        /// <param name="value">The value.</param>
        /// <returns>True iff a configuration entry with the specified unique key was found and could be converted to a double.</returns>
        public bool TryGetValueAsDouble(string key, out double value)
        {
            string strValue;
            value = 0;
            if(TryGetValue(key, out strValue))
            {
                return double.TryParse(strValue, out value);
            }

            return false;
        }
    }
}
