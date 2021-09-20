using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace PerfView
{
    /// <summary>
    /// Stack viewer preset that includes information about grouping and folding patterns,
    /// folding percentage.
    /// </summary>
    public class Preset : IEquatable<Preset>
    {
        public string Name { get; set; }
        public string GroupPat { get; set; }
        public string FoldPercentage { get; set; }
        public string FoldPat { get; set; }
        public string Comment { get; set; }

        /// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(Preset other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Name, other.Name) &&
                   string.Equals(GroupPat, other.GroupPat) &&
                   string.Equals(FoldPercentage, other.FoldPercentage) &&
                   string.Equals(FoldPat, other.FoldPat) &&
                   string.Equals(Comment, other.Comment);
        }

        /// <summary>
        /// Parses collection of presets kept as a string
        /// </summary>
        public static List<Preset> LoadPresets()
        {
            string presets = App.ConfigData["Presets"];

            if (presets == null)
            {
                return new List<Preset>(0);
            }
            var entries = presets.Split(new[] { PresetSeparator }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<Preset>(entries.Length);
            foreach (var entry in entries)
            {
                result.Add(ParsePreset(entry));
            }

            return result;
        }

        /// <summary>
        /// Saves all presets into the configuration file.
        /// </summary>
        public static void SavePresets(List<Preset> presets)
        {
            var result = new StringBuilder();
            bool firstPreset = true;
            foreach (var preset in presets)
            {
                if (!firstPreset)
                {
                    result.Append(PresetSeparator);
                }
                firstPreset = false;

                result.Append(Serialize(preset));
            }

            App.ConfigData["Presets"] = result.ToString();
        }

        /// <summary>
        /// Exports presets to a given file.
        /// </summary>
        public static void Export(List<Preset> presets, string fileName)
        {
            using (XmlWriter writer = XmlWriter.Create(
                 fileName,
                 new XmlWriterSettings() { Indent = true, NewLineOnAttributes = true }))
            {
                writer.WriteStartElement("Presets");
                foreach (Preset preset in presets)
                {
                    writer.WriteElementString("Preset", Preset.Serialize(preset));
                }
                writer.WriteEndElement();
            }
        }

        /// <summary>
        /// Imports presets from a file to a given collection.
        /// </summary>
        public static void Import(string fileName, List<Preset> target, Action<Preset> importAction, TextWriter logWriter)
        {
            List<Preset> presetsFromFile = new List<Preset>();
            XmlReaderSettings settings = new XmlReaderSettings() { IgnoreWhitespace = true, IgnoreComments = true };
            using (XmlReader reader = XmlTextReader.Create(fileName, settings))
            {
                int entryDepth = reader.Depth;
                try
                {
                    reader.Read();
                    while (true)
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Depth > entryDepth)
                        {
                            string value = reader.ReadElementContentAsString();
                            presetsFromFile.Add(Preset.ParsePreset(value));
                            continue;
                        }

                        if (!reader.Read())
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine($"[Import of presets from {fileName} has failed.]");
                    logWriter.WriteLine("Error during reading presets file: " + ex);
                }
            }

            // Now we have current presets in 'target' collection and new presets in presetsFromFile collection.
            // Existing identical presets are ignored.
            // Existing presets that differ are ignored too, but warning is written into logs.
            // For imported presets an action is called
            int imported = 0, existing = 0, ignored = 0;
            foreach (var preset in presetsFromFile)
            {
                var existingPreset = target.FirstOrDefault(x => x.Name == preset.Name);
                if (existingPreset == null)
                {
                    target.Add(preset);
                    importAction?.Invoke(preset);
                    imported++;
                    continue;
                }

                if (existingPreset.Equals(preset))
                {
                    existing++;
                    continue;
                }

                logWriter.WriteLine($"WARN: Preset '{preset.Name}' was ignored during import because there already exist a different preset with the same name.");
                ignored++;
            }
            logWriter.WriteLine($"[Import of presets completed: {imported} imported, {existing} existed, {ignored} ignored.]");
        }
 
        private static string Serialize(Preset preset)
        {
            var result = new StringBuilder();
            result.Append("Name=" + preset.Name + PartSeparator);
            result.Append("GroupPat=" + preset.GroupPat + PartSeparator);
            result.Append("FoldPercentage=" + preset.FoldPercentage + PartSeparator);
            result.Append("FoldPat=" + preset.FoldPat + PartSeparator);
            result.Append("Comment=" + XmlConvert.EncodeName(preset.Comment));
            return result.ToString();
        }

        private static Preset ParsePreset(string presetString)
        {
            var preset = new Preset();
            var presetParts = presetString.Split(new[] { PartSeparator }, StringSplitOptions.None);
            foreach (var presetPart in presetParts)
            {
                int separatorIndex = presetPart.IndexOf('=');
                string partName = presetPart.Substring(0, separatorIndex);
                string partValue = presetPart.Substring(separatorIndex + 1);
                switch (partName)
                {
                    case "Name":
                        preset.Name = partValue;
                        break;
                    case "GroupPat":
                        preset.GroupPat = partValue;
                        break;
                    case "FoldPercentage":
                        preset.FoldPercentage = partValue;
                        break;
                    case "FoldPat":
                        preset.FoldPat = partValue;
                        break;
                    case "Comment":
                        preset.Comment = XmlConvert.DecodeName(partValue);
                        break;
                }
            }

            return preset;
        }



        private const string PresetSeparator = "####";
        private const string PartSeparator = "$$$$";
    }
}
