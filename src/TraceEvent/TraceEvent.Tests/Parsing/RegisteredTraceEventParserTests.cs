using Microsoft.Diagnostics.Tracing.Parsers;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    }
}
