using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using LinuxTracing.LinuxTraceEvent;

namespace LinuxTracing.Tests
{
	public class Program
	{
		public static void Main(string[] args)
		{
			string pattern = null;
			int maxSamples = 50000;
			bool doBlockedTime = false;

			for (int i = 1; args.Length > 1 && i < args.Length; i++)
			{
				if (args[i].Length > 2)
				{
					string part = args[i].Substring(0, 2);
					if (part == "-p")
					{
						pattern = args[i].Substring(3);
					}
					else if (part == "-m")
					{
						maxSamples = int.Parse(args[i].Substring(3));
					}
					else if (args[i] == "--threadtime")
					{
						doBlockedTime = true;
					}
				}
			}

			PerfScriptEventParser parser = new PerfScriptEventParser(args[0], doBlockedTime);
			parser.Parse(
				pattern: pattern,
				maxSamples: maxSamples);
			Program.TranslateToPerfViewXml(args[0], parser);
		}

		private static void TranslateToPerfViewXml(string filename, PerfScriptEventParser parser)
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
					writer.WriteAttributeString("Time", string.Format("{0:0.000}", 1000 * parser.GetTimeInSecondsAtSample(i)));
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
