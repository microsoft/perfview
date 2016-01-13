using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using LinuxPerfView.LinuxTraceEvent;

namespace LinuxPerfView
{
	public class Program
	{
		public static void Main(string[] args)
		{
			PerfScriptTraceEventParser parser = new PerfScriptTraceEventParser(@"C:\Users\t-lufern\Desktop\Luca\dev\perf.data.dump");
			parser.Parse();
			Program.TranslateToPerfViewXml(parser);
		}

		private static void TranslateToPerfViewXml(PerfScriptTraceEventParser parser)
		{
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.Indent = true;
			settings.IndentChars = "    ";
			//settings.NewLineOnAttributes = true;

			using (XmlWriter writer = XmlWriter.Create(@"C:\Users\t-lufern\Desktop\Luca\dev\test.xml", settings))
			{
				writer.WriteStartElement("StackWindow");
				writer.WriteStartElement("StackSource");

				// Frames
				WriteElementCount(writer, "Frames", parser.FrameID, delegate (int i)
				{
					// Frames start at 1 not at 0 for now...
					if (i == 0) return;

					writer.WriteStartElement("Frame");
					writer.WriteAttributeString("ID", i.ToString());
					writer.WriteString(parser.IDToFrame[i]);
					writer.WriteEndElement();
				});

				// Stacks
				WriteElementCount(writer, "Stacks", parser.StackID, delegate (int i)
				{
					writer.WriteStartElement("Stack");
					writer.WriteAttributeString("ID", i.ToString());
					writer.WriteAttributeString("CallerID", parser.Stacks[i].Value.ToString());
					writer.WriteAttributeString("FrameID", parser.Stacks[i].Key.ToString());
					writer.WriteEndElement();
				});

				// Samples
				WriteElementCount(writer, "Samples", parser.SampleID, delegate (int i)
				{
					writer.WriteStartElement("Sample");
					writer.WriteAttributeString("ID", i.ToString());
					writer.WriteAttributeString("Time", string.Format("{0:0.000}", parser.Samples[i].Value));
					writer.WriteAttributeString("SampleID", parser.Samples[i].Key.ToString());
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
