using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using LinuxEvent.LinuxTraceEvent;

namespace LinuxEvent
{
	public class Program
	{
		public static void Main(string[] args)
		{
			PerfScriptTraceEventParser parser = new PerfScriptTraceEventParser(args[0]);
			parser.Parse(regexFilter: (args.Length > 1 ? args[1] : null), maxSamples: (args.Length > 2 ? int.Parse(args[2]) : 50000));
			Program.TranslateToPerfViewXml(args[0], parser);
		}

		private static void TranslateToPerfViewXml(string filename, PerfScriptTraceEventParser parser)
		{
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.Indent = true;
			settings.IndentChars = " ";

			using (XmlWriter writer = XmlWriter.Create(filename + ".perfView.xml", settings))
			{
				writer.WriteStartElement("StackWindow");
				writer.WriteStartElement("StackSource");

				// Frames
				WriteElementCount(writer, "Frames", parser.FrameCount, delegate (int i)
				{
					writer.WriteStartElement("Frame");
					writer.WriteAttributeString("ID", i.ToString());
					writer.WriteString(parser.GetFrameAt(i));
					writer.WriteEndElement();
				});

				// Stacks
				WriteElementCount(writer, "Stacks", parser.StackCount, delegate (int i)
				{
					writer.WriteStartElement("Stack");
					writer.WriteAttributeString("ID", i.ToString());
					writer.WriteAttributeString("CallerID", parser.GetCallerAtStack(i).ToString());
					writer.WriteAttributeString("FrameID", parser.GetFrameAtStack(i).ToString());
					writer.WriteEndElement();
				});

				// Samples
				WriteElementCount(writer, "Samples", parser.SampleCount, delegate (int i)
				{
					writer.WriteStartElement("Sample");
					writer.WriteAttributeString("ID", i.ToString());
					writer.WriteAttributeString("Time", string.Format("{0:0.000}", parser.GetTimeAtSample(i)));
					writer.WriteAttributeString("StackID", parser.GetStackAtSample(i).ToString());
					writer.WriteEndElement();

				});

				// End
				writer.WriteEndElement();
				writer.WriteEndElement();
				writer.Flush();
			}
		}

		private static void WriteElementCount(XmlWriter writer, string section, int count, Action<int> perElement)
		{
			writer.WriteStartElement(section);
			writer.WriteAttributeString("Count", count.ToString());

			for (int i = 0; i < count; i++)
			{
				perElement(i);
			}

			writer.WriteEndElement();
		}
	}
}
