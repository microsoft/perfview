namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// A process contained in a trace.
    /// </summary>
    public class Process
    {
        /// <summary>
        /// Create an instance with default values.
        /// </summary>
        public Process()
        {
        }

        /// <summary>
        /// Create an instance with the specified values.
        /// </summary>
        /// <param name="uniqueID">The unique id for the process.</param>
        /// <param name="displayID">The display id for the process.</param>
        /// <param name="description">The string description for the process.</param>
        /// <param name="containsManagedCode">Whether or not the process contains any managed code.</param>
        public Process(int uniqueID, int displayID, string description, bool containsManagedCode)
        {
            UniqueID = uniqueID;
            DisplayID = displayID;
            Description = description;
            ContainsManagedCode = containsManagedCode;
        }

        /// <summary>
        /// The unique ID of the process.
        /// </summary>
        public int UniqueID { get; set; }

        /// <summary>
        /// The ID that should be used for display purposes.
        /// </summary>
        public int DisplayID { get; set; }

        /// <summary>
        /// The description for the process.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// True iff the process contains managed code.
        /// </summary>
        public virtual bool ContainsManagedCode { get; }

        public override int GetHashCode()
        {
            return UniqueID;
        }

        public override bool Equals(object obj)
        {
            if(obj is Process)
            {
                return UniqueID == ((Process)obj).UniqueID;
            }

            return false;
        }
    }
}
