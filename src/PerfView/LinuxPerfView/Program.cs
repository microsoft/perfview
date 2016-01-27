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


				writer.WriteEndElement();


				// Write window state
				Program.WriteCommonWindowState(writer);


				// End
				writer.WriteEndElement();
				writer.Flush();
			}
		}

		private static void WriteCommonWindowState(XmlWriter writer)
		{
			writer.WriteStartElement("StackWindowGuiState");

			writer.WriteStartElement("FilterGuiState");

			writer.WriteStartElement("Scenarios");
			writer.WriteEndElement();


			writer.WriteStartElement("GroupRegEx");

			writer.WriteStartElement("Value");
			writer.WriteString(@"[Just My App]           \ver.2016-01-04.14.32.31.276\%!-&gt;;!=&gt;OTHER");
			writer.WriteEndElement();



			writer.WriteStartElement("History");

			writer.WriteStartElement("string");
			writer.WriteString(@"[Just My App]           \ver.2016 - 01 - 04.14.32.31.276\% !-&gt; ;!= &gt; OTHER");
			writer.WriteEndElement();

			writer.WriteStartElement("string");
			writer.WriteString(@"[no grouping]");
			writer.WriteEndElement();

			writer.WriteStartElement("string");
			writer.WriteString(@"[group CLR/OS entries] \Temporary ASP.NET Files\-&gt;;v4.0.30319\%!=&gt;CLR;v2.0.50727\%!=&gt;CLR;mscoree=&gt;CLR;\mscorlib.*!=&gt;LIB;\System.*!=&gt;LIB;Presentation%=&gt;WPF;WindowsBase%=&gt;WPF;system32\*!=&gt;OS;syswow64\*!=&gt;OS;{%}!=&gt; module $1");
			writer.WriteEndElement();

			writer.WriteStartElement("string");
			writer.WriteString(@"[group modules]           {%}!-&gt;module $1");
			writer.WriteEndElement();

			writer.WriteStartElement("string");
			writer.WriteString(@"[group module entries]  {%}!=&gt;module $1");
			writer.WriteEndElement();

			writer.WriteStartElement("string");
			writer.WriteString(@"[group full path module entries]  {*}!=&gt;module $1");
			writer.WriteEndElement();

			writer.WriteStartElement("string");
			writer.WriteString(@"[group class entries]     {%!*}.%(=&gt;class $1;{%!*}::=&gt;class $1");
			writer.WriteEndElement();

			writer.WriteStartElement("string");
			writer.WriteString(@"[group classes]            {%!*}.%(-&gt;class $1;{%!*}::-&gt;class $1");
			writer.WriteEndElement();

			writer.WriteEndElement();

			writer.WriteEndElement();


			writer.WriteStartElement("FoldPercent");
			writer.WriteStartElement("Value");
			writer.WriteString("1");
			writer.WriteEndElement();
			writer.WriteEndElement();

			writer.WriteStartElement("FoldRegEx");
			writer.WriteStartElement("Value");
			writer.WriteString("ntoskrnl!%ServiceCopyEnd");
			writer.WriteEndElement();

			writer.WriteStartElement("History");
			writer.WriteStartElement("string");
			writer.WriteString("ntoskrnl!%ServiceCopyEnd");
			writer.WriteEndElement();
			writer.WriteEndElement();

			writer.WriteEndElement();

			writer.WriteStartElement("ExcludeRegEx");
			writer.WriteEndElement();

			writer.WriteStartElement("TypePriority");
			writer.WriteEndElement();

			writer.WriteEndElement();

			writer.WriteStartElement("Notes");
			writer.WriteString("Notes typed here will be saved when the view is saved. F2 will hide/unhide.");
			writer.WriteEndElement();

			writer.WriteEndElement();
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
