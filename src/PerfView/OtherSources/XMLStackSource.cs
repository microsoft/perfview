using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Diagnostics.Tracing.StackSources
{
    public class XmlStackSourceWriter
    {
        public static void WriteStackViewAsZippedXml(StackSource source, string fileName, Action<XmlWriter> additionalData = null)
        {
            if (File.Exists(fileName))
                File.Delete(fileName);
            using (var archive =  ZipFile.Open(fileName, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(Path.GetFileNameWithoutExtension(fileName));
                using (var entryStream = entry.Open())
                    WriteStackViewAsXml(source, entryStream, additionalData);
            }
        }
        public static void WriteStackViewAsXml(StackSource source, string fileName, Action<XmlWriter> additionalData = null)
        {
            if (File.Exists(fileName))
                File.Delete(fileName);
            using (var writeStream = File.Create(fileName))
                WriteStackViewAsXml(source, writeStream, additionalData);
        }
        public static void WriteStackViewAsXml(StackSource source, Stream writeStream, Action<XmlWriter> additionalData = null)
        {
            using (var writer = XmlWriter.Create(writeStream, new XmlWriterSettings() { Indent = true, IndentChars = " " }))
            {
                writer.WriteStartElement("StackWindow");
                XmlStackSourceWriter.WriteStacks(source, writer);

                if (additionalData != null)
                    additionalData(writer);
                writer.WriteEndElement();
            }
        }

        public static void WriteStacks(StackSource source, XmlWriter writer)
        {
            writer.WriteStartElement("StackSource");
            writer.WriteStartElement("Frames");
            writer.WriteAttributeString("Count", source.CallFrameIndexLimit.ToString());
            for (int i = 0; i < source.CallFrameIndexLimit; i++)
            {
                writer.WriteStartElement("Frame");
                writer.WriteAttributeString("ID", i.ToString());
                var frameName = source.GetFrameName((StackSourceFrameIndex)i, true);

                writer.WriteString(frameName);
                writer.WriteEndElement();   // Frame
            }
            writer.WriteEndElement();   // Frames

            writer.WriteStartElement("Stacks");
            writer.WriteAttributeString("Count", source.CallStackIndexLimit.ToString());
            for (int i = 0; i < source.CallStackIndexLimit; i++)
            {
                writer.WriteStartElement("Stack");
                writer.WriteAttributeString("ID", i.ToString());
                var FrameID = source.GetFrameIndex((StackSourceCallStackIndex)i);
                var callerID = source.GetCallerIndex((StackSourceCallStackIndex)i);
                writer.WriteAttributeString("CallerID", ((int)callerID).ToString());
                writer.WriteAttributeString("FrameID", ((int)FrameID).ToString());
                writer.WriteEndElement();   // Stack
            }
            writer.WriteEndElement();   // Stacks

            writer.WriteStartElement("Samples");
            writer.WriteAttributeString("Count", source.SampleIndexLimit.ToString());
            // We use the invariant culture, otherwise if we encode in France and decode 
            // in English we get parse errors (this happened!);
            var invariantCulture = CultureInfo.InvariantCulture;
            source.ForEach(delegate(StackSourceSample sample)
            {
                // <Sample ID="1" Time="3432.23" StackID="2" Metric="1" EventKind="CPUSample" />
                writer.WriteStartElement("Sample");
                writer.WriteAttributeString("ID", ((int)sample.SampleIndex).ToString());
                writer.WriteAttributeString("Time", sample.TimeRelativeMSec.ToString("f3", invariantCulture));
                writer.WriteAttributeString("StackID", ((int)sample.StackIndex).ToString());
                if (sample.Metric != 1)
                {
                    var asInt = (int)sample.Metric;
                    if (sample.Metric == asInt)
                        writer.WriteAttributeString("Metric", asInt.ToString());
                    else
                        writer.WriteAttributeString("Metric", sample.Metric.ToString("f3", invariantCulture));
                }
                writer.WriteEndElement();
            });
            writer.WriteEndElement(); // Samples
            writer.WriteEndElement(); // StackSource
        }
    }

    /// <summary>
    /// Reads a very reasonable XML encoding of a stack source. 
    /// 
    /// </summary>
    public class XmlStackSource : StackSource
    {
        /// <summary>
        /// Generates a Stack Source from an XML file created with XmlStackSourceWriter.
        /// If 'readElement' is non-null Any XML Elements that are not recognised to it so 
        /// that that information can be parsed by upper level logic.  When that routine
        /// returns it must have skipped past that element (so reader points at whatever 
        /// is after the End element tag).  
        /// 
        /// If the filename ends in .zip, the file is assumed to be a ZIPPed XML file and
        /// it is first Unziped and then processed.  
        /// </summary>
        public XmlStackSource(string fileName, Action<XmlReader> readElement = null)
        {
            using (Stream dataStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                var xmlStream = dataStream;
                if (fileName.EndsWith(".zip"))
                {
                    var zipArchive = new ZipArchive(dataStream);
                    var entries = zipArchive.Entries;
                    if (entries.Count != 1)
                        throw new ApplicationException("The ZIP file does not have exactly 1 XML file in it,");
                    xmlStream = entries[0].Open();
                }

                XmlReaderSettings settings = new XmlReaderSettings() { IgnoreWhitespace = true, IgnoreComments = true };
                XmlReader reader = XmlTextReader.Create(xmlStream, settings);
                if (!reader.ReadToDescendant("StackWindow"))
                    throw new ApplicationException("The file " + fileName + " does not have a StackWindow element");

                reader.Read();      // Skip the StackWindow element. 
                for(;;)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "StackSource")
                            Read(reader);
                        else if (readElement != null)
                            readElement(reader);
                        else
                            reader.Skip();
                    }
                    else if (!reader.Read())
                        break;
                }
            }
        }

        // TODO intern modules 
        public override void ForEach(Action<StackSourceSample> callback)
        {
            for (int i = 0; i < m_samples.Length; i++)
                callback(m_samples[i]);
        }
        public override bool SamplesImmutable { get { return true; } }
        public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
        {
            return (StackSourceCallStackIndex)m_stacks[(int)callStackIndex].callerID;
        }
        public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
        {
            return (StackSourceFrameIndex)m_stacks[(int)callStackIndex].frameID;
        }
        public override string GetFrameName(StackSourceFrameIndex frameIndex, bool verboseName)
        {
            var ret = m_frames[(int)frameIndex];
            if (!verboseName)
            {
                if (m_shortFrameNames == null)
                    m_shortFrameNames = new string[m_frames.Length];
                var shortName = m_shortFrameNames[(int)frameIndex];
                if (shortName == null)
                {
                    shortName = ret;
                    // Strip off anything before the last \\ before the !
                    var exclaimIdx = ret.IndexOf('!');
                    if (0 < exclaimIdx)
                    {
                        // Becomes 0 if it fails, which is what we want. 
                        var startIdx = ret.LastIndexOf('\\', exclaimIdx - 1, exclaimIdx - 1) + 1;   
                        shortName = ret.Substring(startIdx);
                    }
                    m_shortFrameNames[(int)frameIndex] = shortName;
                }
                ret = shortName;
            }
            return ret;
        }
        public override int CallStackIndexLimit
        {
            get { return m_stacks.Length; }
        }
        public override int CallFrameIndexLimit
        {
            get { return m_frames.Length; }
        }
        public override int SampleIndexLimit
        {
            get { return m_samples.Length; }
        }
        public override double SampleTimeRelativeMSecLimit
        {
            get { return m_maxTime; }
        }
        public override StackSourceSample GetSampleByIndex(StackSourceSampleIndex sampleIndex)
        {
            return m_samples[(int)sampleIndex];
        }

        #region private
        private void Read(XmlReader reader)
        {
            // We use the invarient culture, otherwise if we encode in france and decode 
            // in english we get parse errors (this happened!);
            var invariantCulture = CultureInfo.InvariantCulture;
            var inputDepth = reader.Depth;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name == "Sample")
                        {
                            var sample = new StackSourceSample(this);
                            sample.Metric = 1;
                            if (reader.MoveToFirstAttribute())
                            {
                                do
                                {
                                    if (reader.Name == "ID")
                                        sample.SampleIndex = (StackSourceSampleIndex)reader.ReadContentAsInt();
                                    else if (reader.Name == "Time")
                                        sample.TimeRelativeMSec = double.Parse(reader.ReadContentAsString(), invariantCulture);
                                    else if (reader.Name == "StackID")
                                        sample.StackIndex = (StackSourceCallStackIndex)reader.ReadContentAsInt();
                                    else if (reader.Name == "Metric")
                                        sample.Metric = float.Parse(reader.ReadContentAsString(), invariantCulture);
                                } while (reader.MoveToNextAttribute());
                            }
                            m_samples[m_curSample++] = sample;
                            if (sample.TimeRelativeMSec > m_maxTime)
                                m_maxTime = sample.TimeRelativeMSec;
                        }
                        if (reader.Name == "Stack")
                        {
                            var stackID = -1;
                            var callerID = -1;
                            var frameID = -1;
                            if (reader.MoveToFirstAttribute())
                            {
                                do
                                {
                                    if (reader.Name == "ID")
                                        stackID = reader.ReadContentAsInt();
                                    else if (reader.Name == "FrameID")
                                        frameID = reader.ReadContentAsInt();
                                    else if (reader.Name == "CallerID")
                                        callerID = reader.ReadContentAsInt();
                                } while (reader.MoveToNextAttribute());
                            }
                            m_stacks[stackID].frameID = frameID;
                            m_stacks[stackID].callerID = callerID;
                        }
                        else if (reader.Name == "Frame")
                        {
                            var frameID = -1;
                            if (reader.MoveToFirstAttribute())
                            {
                                do
                                {
                                    if (reader.Name == "ID")
                                        frameID = reader.ReadContentAsInt();
                                } while (reader.MoveToNextAttribute());
                            }
                            reader.Read();      // Move on to body of the element
                            var frameName = reader.ReadContentAsString();
                            m_frames[frameID] = frameName;
                        }
                        else if (reader.Name == "Frames")
                        {
                            var count = reader.GetAttribute("Count");
                            m_frames = new string[int.Parse(count)];
                        }
                        else if (reader.Name == "Stacks")
                        {
                            var count = reader.GetAttribute("Count");
                            m_stacks = new Frame[int.Parse(count)];
#if DEBUG
                            for (int i = 0; i < m_stacks.Length; i++)
                            {
                                m_stacks[i].frameID = int.MinValue;
                                m_stacks[i].callerID = int.MinValue;
                            }
#endif
                        }
                        else if (reader.Name == "Samples")
                        {
                            var count = reader.GetAttribute("Count");
                            m_samples = new StackSourceSample[int.Parse(count)];
                        }
                        break;
                    case XmlNodeType.EndElement:
                        if (reader.Depth <= inputDepth)
                        {
                            reader.Read();
                            goto Done;
                        }
                        break;
                    case XmlNodeType.Text:
                    default:
                        break;
                }
            }
        Done: ;
#if DEBUG
            Debug.Assert(m_samples != null && m_frames != null && m_stacks != null);
            for (int i = 0; i < m_samples.Length; i++)
                Debug.Assert(m_samples[i] != null);
            for (int i = 0; i < m_frames.Length; i++)
                Debug.Assert(m_frames[i] != null);
            for (int i = 0; i < m_stacks.Length; i++)
            {
                Debug.Assert(m_stacks[i].frameID >= 0);
                Debug.Assert(m_stacks[i].callerID >= -1);
            }
#endif
        }

        struct Frame
        {
            public int callerID;
            public int frameID;
        }

        string[] m_shortFrameNames;
        string[] m_frames;
        Frame[] m_stacks;
        StackSourceSample[] m_samples;
        int m_curSample;
        double m_maxTime;
        #endregion
    }
}