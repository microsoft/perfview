using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class RegisteredTraceEventParserTests
    {
        private readonly ITestOutputHelper _output;

        public RegisteredTraceEventParserTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Test that GetManifestForRegisteredProvider does not produce duplicate string IDs in the stringTable.
        /// This test uses the Microsoft-JScript provider which is known to exist on all Windows machines.
        /// </summary>
        [WindowsFact]
        public void GetManifestForRegisteredProvider_NoDuplicateStringTableEntries()
        {
            // Microsoft-JScript is a well-known provider that exists on all Windows machines
            const string providerName = "Microsoft-JScript";

            // Get the manifest for the provider
            string manifest = RegisteredTraceEventParser.GetManifestForRegisteredProvider(providerName);

            Assert.NotNull(manifest);
            Assert.NotEmpty(manifest);

            _output.WriteLine($"Generated manifest for {providerName} (length: {manifest.Length} chars)");

            // Extract all string IDs from the manifest's stringTable
            // The format is: <string id="..." value="..."/>
            var stringIdPattern = new Regex(@"<string\s+id=""([^""]+)""", RegexOptions.Compiled);
            var matches = stringIdPattern.Matches(manifest);

            var stringIds = new List<string>();
            foreach (Match match in matches)
            {
                stringIds.Add(match.Groups[1].Value);
            }

            _output.WriteLine($"Found {stringIds.Count} string entries in stringTable");

            // Check for duplicates
            var duplicates = stringIds
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToList();

            if (duplicates.Any())
            {
                _output.WriteLine($"Found {duplicates.Count} duplicate string IDs:");
                foreach (var dup in duplicates)
                {
                    _output.WriteLine($"  '{dup.Id}' appears {dup.Count} times");
                }
            }

            // Assert no duplicates exist
            Assert.Empty(duplicates);
        }

        /// <summary>
        /// Test that GetManifestForRegisteredProvider properly escapes XML special characters
        /// in attribute values and text content. This test uses the Microsoft-Windows-Ntfs provider
        /// which is known to have values containing quotes and angle brackets.
        /// </summary>
        [WindowsFact]
        public void GetManifestForRegisteredProvider_ProperlyEscapesXmlCharacters()
        {
            // Microsoft-Windows-Ntfs is a well-known provider with complex metadata
            const string providerName = "Microsoft-Windows-Ntfs";

            // Get the manifest for the provider
            string manifest = RegisteredTraceEventParser.GetManifestForRegisteredProvider(providerName);

            Assert.NotNull(manifest);
            Assert.NotEmpty(manifest);

            _output.WriteLine($"Generated manifest for {providerName} (length: {manifest.Length} chars)");

            // Verify the manifest is well-formed XML by parsing it
            var xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.LoadXml(manifest);
                _output.WriteLine("✓ Manifest is well-formed XML");
            }
            catch (XmlException ex)
            {
                _output.WriteLine($"✗ Manifest XML parsing failed: {ex.Message}");
                _output.WriteLine($"Line {ex.LineNumber}, Position {ex.LinePosition}");
                
                // Show context around the error
                var lines = manifest.Split('\n');
                if (ex.LineNumber > 0 && ex.LineNumber <= lines.Length)
                {
                    int start = Math.Max(0, ex.LineNumber - 3);
                    int end = Math.Min(lines.Length, ex.LineNumber + 2);
                    _output.WriteLine("\nContext:");
                    for (int i = start; i < end; i++)
                    {
                        string marker = (i == ex.LineNumber - 1) ? ">>> " : "    ";
                        _output.WriteLine($"{marker}{i + 1}: {lines[i]}");
                    }
                }
                
                throw;
            }

            // Check that all attribute values with special characters are properly escaped
            // by verifying we can successfully query the XML document
            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("e", "http://schemas.microsoft.com/win/2004/08/events");
            nsmgr.AddNamespace("win", "http://manifests.microsoft.com/win/2004/08/windows/events");

            // Verify we can access various elements that might contain special characters
            var keywords = xmlDoc.SelectNodes("//e:keyword", nsmgr);
            var tasks = xmlDoc.SelectNodes("//e:task", nsmgr);
            var opcodes = xmlDoc.SelectNodes("//e:opcode", nsmgr);
            var valueMaps = xmlDoc.SelectNodes("//e:valueMap", nsmgr);
            var bitMaps = xmlDoc.SelectNodes("//e:bitMap", nsmgr);
            var stringElements = xmlDoc.SelectNodes("//e:string", nsmgr);

            _output.WriteLine($"Found {keywords?.Count ?? 0} keywords");
            _output.WriteLine($"Found {tasks?.Count ?? 0} tasks");
            _output.WriteLine($"Found {opcodes?.Count ?? 0} opcodes");
            _output.WriteLine($"Found {valueMaps?.Count ?? 0} valueMaps");
            _output.WriteLine($"Found {bitMaps?.Count ?? 0} bitMaps");
            _output.WriteLine($"Found {stringElements?.Count ?? 0} string entries");

            // If we got here, the XML was successfully parsed and queried
        }
    }
}
