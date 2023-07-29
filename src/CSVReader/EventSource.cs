using Microsoft.Diagnostics.Tracing.TraceUtilities.FilterQueryExpression;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EventSources
{

    /// <summary>
    /// An EventSource is an abstraction that provides all the functionality that is needed by PerfView's Events viewer GUI.
    /// Anything that implements this interface can be viewed in the Events Viewer.
    /// 
    /// The main functionality that subclasses implement is the 'Events' enumeration.   
    /// </summary>
    public abstract class EventSource
    {
        /* ForEach is the main attraction.   Everything else simply supports this */
        /// <summary>
        /// ForEach is the most important property, calls 'callback' for each event in turn.
        /// If the callback return false, then the foreach does not deliver any more callbacks.  
        /// </summary>
        public abstract void ForEach(Func<EventRecord, bool> callback);

        // filtering.  These should be consulted by the subclasses when returning events in the 'Events' enumeration.  
        /// <summary>
        /// Filter out any events less than StartTimeRelativeMSec
        /// </summary>
        public double StartTimeRelativeMSec;
        /// <summary>
        /// Filter out any events greater than EndTimeRelativeMSec
        /// </summary>
        public double EndTimeRelativeMSec;
        /// <summary>
        /// The maximum number of events to return.    Will return an eventrecord with a null EventName after this (but a valid timestamp).  
        /// </summary>
        public int MaxRet;
        /// <summary>
        /// If set, only display events from a process that matches this regular expression
        /// </summary>
        public string ProcessFilterRegex;
        /// <summary>
        /// The list of all process names (exe names without the .EXE) in the collection (optional, null if not known)) 
        /// </summary>
        public virtual ICollection<string> ProcessNames { get { return null; } }
        /// <summary>
        /// If set, only display events that have this regular expression in their 'ToString' (this is relative expensive)
        /// </summary>
        public string TextFilterRegex;
        /// <summary>
        /// After calling this method with a list of event Names, the Events enumeration will only return
        /// records of events with one of these names.  
        /// </summary>
        public abstract void SetEventFilter(List<string> eventNames);
        /// <summary>
        /// returns the set of names for events in the collection (used to populate the left pane in the display.  
        /// </summary>
        public abstract ICollection<string> EventNames { get; }

        // selecting the columns to return. 
        /// <summary>
        /// Set the columns you wish to show up as Field1, Field2, ...
        /// Use AllColumnNames to get a valid list of names to use here.  
        /// </summary>
        public List<string> ColumnsToDisplay;
        /// <summary>
        /// Optionally you can provide a list of columns available.  
        /// </summary>
        public virtual ICollection<string> AllColumnNames(List<string> eventNames) { return null; }
        /// <summary>
        /// An optional filter query expression tree that'll be used to filter different events. 
        /// </summary>
        public FilterQueryExpressionTree FilterQueryExpressionTree { get; set; } = null;

        /// <summary>
        /// The number of fields that are NOT put in the 'rest' column.  Defaults to 4
        /// </summary>
        public int NonRestFields;

        /// <summary>
        /// Optionally, the implementation can provide the sum of every entry in each column specified in 'ColumnsToDisplay'. 
        /// This will be set by the implementation when the 'Events' enumeration is scanned. 
        /// </summary>
        public virtual double[] ColumnSums { get; protected set; }
        /// <summary>
        /// If the source knows a bound on the times of all the events it sets this.  Should be set to PositiveInfinity if it is unknown.
        /// </summary>
        public double MaxEventTimeRelativeMsec;

        /// <summary>
        /// Clones the EventSource, so you can have multiple windows opened on the same set of data.   
        /// </summary>
        /// <returns></returns>
        public abstract EventSource Clone();

        /// <summary>
        /// Utility function that takes a specification for columns (which can include *) and the raw list of
        /// all possible columns and returns the list that match the specification. This function also extracts the event filter query and creates a FilterQueryExpressionTree.
        /// </summary>
        public static List<string> ParseColumns(string columnSpec, ICollection<string> columnNames)
        {
            if (string.IsNullOrWhiteSpace(columnSpec))
            {
                return null;
            }

            var ret = new List<string>();
            var regex = new Regex(@"\s*(\S+)\s*");
            var index = 0;
            for (; ; )
            {
                var match = regex.Match(columnSpec, index);
                var name = match.Groups[1].Value;
                if (name == "*")
                {
                    var startCount = ret.Count;
                    foreach (var colName in columnNames)
                    {
                        // If it was already specified, leave it out
                        for (int i = 0; i < startCount; i++)
                        {
                            if (ret[i] == colName)
                            {
                                goto Next;
                            }
                        }

                        ret.Add(colName);
                        Next:;
                    }
                }
                else
                {
                    ret.Add(name);
                }

                index += match.Groups[0].Length;
                if (index == columnSpec.Length)
                {
                    break;
                }
            }
            return ret;
        }
    }

    /// <summary>
    /// An EventRecord is a abstraction that is returned by the EventSource.Events API.   It represents
    /// a single event and is everything the GUI needs to display the event in the GUI.   
    /// </summary>
    public abstract class EventRecord
    {
        public abstract string EventName { get; }
        public abstract string ProcessName { get; }
        public abstract double TimeStampRelatveMSec { get; }

        // TODO FIX NOW should be abstract, get CSV and ETW subclasses to implement
        /// <summary>
        /// The names of the fields in this record
        /// </summary>
        public virtual string[] FieldNames { get { return null; } }
        // TODO FIX NOW should be abstract, get CSV and ETW subclasses to implement
        /// <summary>
        /// This fetches fields from the record.  The index corresponds to the FieldNames array.
        /// </summary>
        public virtual string Field(int index) { return null; }

        /// <summary>
        /// The current contract is that the array is undefined above the 'non-rest' fields (currently we have a max of 4).  
        /// </summary>
        public string[] DisplayFields { get { return m_displayFields; } }
        //  TODO FIX NOW should not be virtual.  
        /// <summary>
        /// Displays fields as key-value pairs.  
        /// </summary>
        public virtual string Rest { get { return m_displayFields[11]; } set { m_displayFields[11] = value; } }
        // The properties are for binding in the GUI.   
        // set property is a hack to allow selection in the GUI (which wants two way binding for that case)
        public string DisplayField1 { get { return m_displayFields[0]; } set { } }
        public string DisplayField2 { get { return m_displayFields[1]; } set { } }
        public string DisplayField3 { get { return m_displayFields[2]; } set { } }
        public string DisplayField4 { get { return m_displayFields[3]; } set { } }
        public string DisplayField5 { get { return m_displayFields[4]; } set { } }
        public string DisplayField6 { get { return m_displayFields[5]; } set { } }
        public string DisplayField7 { get { return m_displayFields[6]; } set { } }
        public string DisplayField8 { get { return m_displayFields[7]; } set { } }
        public string DisplayField9 { get { return m_displayFields[8]; } set { } }
        public string DisplayField10 { get { return m_displayFields[9]; } set { } }

        // returns true of 'pattern' matches the display fields.  
        public virtual bool Matches(Regex pattern)
        {
            // TODO FIX NOW NOT DONE 
            return true;
        }

        #region private 

        protected void SetDisplayFields(EventSource source)
        {
            if (m_displayFields == null)
            {
                m_displayFields = new string[11];
            }

            Debug.Assert(m_displayFields.Length == 11);
            // TODO FIX NOW NOT DONE 
        }

        /// <summary>
        /// m_displayFields are the fields ordered by how the EventSource.ColumnsToDisplay says they should be ordered.
        /// m_displayField[5] is currently the Rest field.  
        /// </summary>
        protected internal string[] m_displayFields;
        protected EventRecord(int numNonRestFields)
        {
            Debug.Assert(numNonRestFields >= 10 || numNonRestFields == 0);
            m_displayFields = new string[numNonRestFields];
        }
        protected EventRecord() { }
        #endregion
    }
}