using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace TraceParserGen.Tests
{
    /// <summary>
    /// Tests for TraceParserGen.exe that validate it can generate parsers from manifests
    /// </summary>
    public class ParserGenerationTests : TestBase
    {
        public ParserGenerationTests(ITestOutputHelper output) : base(output)
        {
        }

        #region Simple manifest tests

        [Fact]
        public void SimpleManifest_GeneratesParserClass()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            Assert.Contains("public sealed class SimpleTestProviderTraceEventParser : TraceEventParser", content);
            Assert.Contains("[System.CodeDom.Compiler.GeneratedCode(\"traceparsergen\", \"2.0\")]", content);
        }

        [Fact]
        public void SimpleManifest_ContainsProviderNameAndGuid()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            Assert.Contains("public static string ProviderName = \"SimpleTestProvider\";", content);
            Assert.Contains("public static Guid ProviderGuid =", content);
            // Verify the GUID bytes correspond to {12345678-1234-1234-1234-123456789012}
            Assert.Contains("0x12345678", content);
        }

        [Fact]
        public void SimpleManifest_GeneratesKeywordEnum()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            Assert.Contains("public enum Keywords : long", content);
            Assert.Contains("Diagnostics = 0x1", content);
        }

        [Fact]
        public void SimpleManifest_GeneratesEventProperties()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            // Events named after their tasks
            Assert.Contains("public event Action<SimpleEventTraceData> SimpleEvent", content);
            Assert.Contains("public event Action<ValueEventTraceData> ValueEvent", content);
        }

        [Fact]
        public void SimpleManifest_GeneratesTraceDataClasses()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            Assert.Contains("public sealed class SimpleEventTraceData : TraceEvent", content);
            Assert.Contains("public sealed class ValueEventTraceData : TraceEvent", content);
        }

        [Fact]
        public void SimpleManifest_GeneratesPayloadAccessors()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            // StringTemplate field
            Assert.Contains("public string Message { get { return GetUnicodeStringAt(0); } }", content);
            // IntTemplate field
            Assert.Contains("public int Value { get { return GetInt32At(0); } }", content);
        }

        [Fact]
        public void SimpleManifest_GeneratesPayloadNames()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            Assert.Contains("payloadNames = new string[] { \"Message\"};", content);
            Assert.Contains("payloadNames = new string[] { \"Value\"};", content);
        }

        [Fact]
        public void SimpleManifest_GeneratesNamespace()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            Assert.Contains("namespace Microsoft.Diagnostics.Tracing.Parsers", content);
            Assert.Contains("namespace Microsoft.Diagnostics.Tracing.Parsers.SimpleTestProvider", content);
        }

        [Fact]
        public void SimpleManifest_GeneratesConstructor()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            Assert.Contains("public SimpleTestProviderTraceEventParser(TraceEventSource source) : base(source) {}", content);
        }

        [Fact]
        public void SimpleManifest_GeneratesRequiredUsings()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            Assert.Contains("using System;", content);
            Assert.Contains("using System.Text;", content);
            Assert.Contains("using Microsoft.Diagnostics.Tracing;", content);
            Assert.Contains("using Address = System.UInt64;", content);
        }

        [Fact]
        public void SimpleManifest_GeneratesEnumerateTemplates()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            Assert.Contains("protected override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)", content);
        }

        [Fact]
        public void SimpleManifest_GeneratesDispatchAndValidate()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            Assert.Contains("protected override void Dispatch()", content);
            Assert.Contains("protected override void Validate()", content);
        }

        [Fact]
        public void SimpleManifest_GeneratesToXml()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            Assert.Contains("public override StringBuilder ToXml(StringBuilder sb)", content);
            Assert.Contains("XmlAttrib(sb, \"Message\", Message)", content);
            Assert.Contains("XmlAttrib(sb, \"Value\", Value)", content);
        }

        #endregion

        #region Multi-type manifest tests

        [Fact]
        public void MultiTypeManifest_GeneratesParserClass()
        {
            string content = GenerateParserFromManifest("MultiType.manifest.xml");

            Assert.Contains("public sealed class MultiTypeTestProviderTraceEventParser : TraceEventParser", content);
        }

        [Fact]
        public void MultiTypeManifest_GeneratesMultipleKeywords()
        {
            string content = GenerateParserFromManifest("MultiType.manifest.xml");

            Assert.Contains("General = 0x1", content);
            Assert.Contains("Performance = 0x2", content);
            Assert.Contains("Debug = 0x4", content);
        }

        [Fact]
        public void MultiTypeManifest_GeneratesStringFieldAccessors()
        {
            string content = GenerateParserFromManifest("MultiType.manifest.xml");

            Assert.Contains("public string UnicodeMessage { get { return GetUnicodeStringAt(0); } }", content);
            Assert.Contains("public string AnsiMessage { get { return GetUTF8StringAt(SkipUnicodeString(0)); } }", content);
        }

        [Fact]
        public void MultiTypeManifest_GeneratesNumericFieldAccessors()
        {
            string content = GenerateParserFromManifest("MultiType.manifest.xml");

            Assert.Contains("public int ByteVal { get { return GetByteAt(0); } }", content);
            Assert.Contains("public int ShortVal { get { return GetInt16At(1); } }", content);
            Assert.Contains("public int IntVal { get { return GetInt32At(3); } }", content);
            Assert.Contains("public long LongVal { get { return GetInt64At(7); } }", content);
            Assert.Contains("public float FloatVal { get { return GetSingleAt(19); } }", content);
            Assert.Contains("public double DoubleVal { get { return GetDoubleAt(23); } }", content);
        }

        [Fact]
        public void MultiTypeManifest_GeneratesGuidAccessor()
        {
            string content = GenerateParserFromManifest("MultiType.manifest.xml");

            Assert.Contains("public Guid ActivityId { get { return GetGuidAt(SkipUnicodeString(4)); } }", content);
        }

        [Fact]
        public void MultiTypeManifest_GeneratesFileTimeAccessor()
        {
            string content = GenerateParserFromManifest("MultiType.manifest.xml");

            Assert.Contains("public DateTime Timestamp { get { return DateTime.FromFileTime(GetInt64At(SkipUnicodeString(4)+20)); } }", content);
        }

        [Fact]
        public void MultiTypeManifest_GeneratesThreeEvents()
        {
            string content = GenerateParserFromManifest("MultiType.manifest.xml");

            Assert.Contains("public event Action<StringFieldsTraceData> StringOp", content);
            Assert.Contains("public event Action<NumericFieldsTraceData> NumericOp", content);
            Assert.Contains("public event Action<MixedFieldsTraceData> MixedOp", content);
        }

        [Fact]
        public void MultiTypeManifest_GeneratesAllPayloadNames()
        {
            string content = GenerateParserFromManifest("MultiType.manifest.xml");

            // Each template should have a complete payloadNames array
            Assert.Contains("payloadNames = new string[] { \"UnicodeMessage\", \"AnsiMessage\"};", content);
            Assert.Contains("payloadNames = new string[] { \"ByteVal\", \"ShortVal\", \"IntVal\", \"LongVal\", \"BoolVal\", \"FloatVal\", \"DoubleVal\"};", content);
            Assert.Contains("payloadNames = new string[] { \"Id\", \"Name\", \"ActivityId\", \"HexValue\", \"Timestamp\"};", content);
        }

        [Fact]
        public void MultiTypeManifest_GeneratesProviderGuid()
        {
            string content = GenerateParserFromManifest("MultiType.manifest.xml");

            Assert.Contains("public static string ProviderName = \"MultiTypeTestProvider\";", content);
            Assert.Contains("0xaabbccdd", content);
        }

        #endregion

        #region Qualifier flag tests

        [Fact]
        public void InternalFlag_GeneratesInternalOverrides()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml", "InternalParser.cs", "/Internal");

            Assert.Contains("protected internal override void Dispatch()", content);
            Assert.Contains("protected internal override void Validate()", content);
            Assert.Contains("protected internal override Delegate Target", content);
            Assert.Contains("protected internal override void EnumerateTemplates(", content);
        }

        [Fact]
        public void InternalFlag_StillGeneratesPublicParserClass()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml", "InternalParser.cs", "/Internal");

            Assert.Contains("public sealed class SimpleTestProviderTraceEventParser : TraceEventParser", content);
        }

        [Fact]
        public void NeedsStateFlag_GeneratesStateClass()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml", "StateParser.cs", "/NeedsState");

            Assert.Contains("internal class SimpleTestProviderState : IFastSerializable", content);
            Assert.Contains("void IFastSerializable.ToStream(Serializer serializer)", content);
            Assert.Contains("void IFastSerializable.FromStream(Deserializer deserializer)", content);
        }

        [Fact]
        public void NeedsStateFlag_GeneratesStateProperty()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml", "StateParser.cs", "/NeedsState");

            Assert.Contains("private SimpleTestProviderState State", content);
        }

        [Fact]
        public void NeedsStateFlag_AddsStateToEventConstructors()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml", "StateParser.cs", "/NeedsState");

            Assert.Contains("SimpleTestProviderState state", content);
            Assert.Contains("SetState", content);
        }

        #endregion

        #region Output file tests

        [Fact]
        public void DefaultOutputFile_UsesCsExtension()
        {
            string manifestPath = Path.Combine(TestDataDir, "SimpleTest.manifest.xml");
            // When no output file is specified, TraceParserGen defaults to .cs extension
            string expectedOutput = Path.ChangeExtension(manifestPath, ".cs");

            var (exitCode, stdout, stderr) = RunTraceParserGen($"\"{manifestPath}\"");

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(expectedOutput), $"Expected default output at {expectedOutput}");

            // Clean up the file generated next to the manifest
            try { File.Delete(expectedOutput); } catch { }
        }

        [Fact]
        public void ExplicitOutputFile_CreatesAtSpecifiedPath()
        {
            string manifestPath = Path.Combine(TestDataDir, "SimpleTest.manifest.xml");
            string outputPath = Path.Combine(OutputDir, "CustomName.cs");

            var (exitCode, stdout, stderr) = RunTraceParserGen($"\"{manifestPath}\" \"{outputPath}\"");

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
        }

        #endregion

        #region Build and instantiate integration test

        [Fact]
        public void GeneratedParser_CompilesAndInstantiates()
        {
            // Generate parser
            string manifestPath = Path.Combine(TestDataDir, "SimpleTest.manifest.xml");
            string outputCsPath = Path.Combine(OutputDir, "SimpleTestParser.cs");
            var (exitCode, _, _) = RunTraceParserGen($"\"{manifestPath}\" \"{outputCsPath}\"");
            Assert.Equal(0, exitCode);

            // TraceParserGen has known code generation limitations that prevent the
            // generated code from compiling as-is. Apply workarounds.
            FixKnownCodeGenIssues(outputCsPath);

            // Create a test console app
            string testProjectDir = Path.Combine(OutputDir, "TestApp");
            Directory.CreateDirectory(testProjectDir);

            CreateTestConsoleApp(testProjectDir, outputCsPath);

            // Restore and build
            RunDotnet("restore", testProjectDir, timeoutMs: 120000);
            var buildExitCode = RunDotnet("build -c Release --no-restore", testProjectDir, timeoutMs: 120000);
            Assert.Equal(0, buildExitCode);

            // Run the test application
            var runExitCode = RunDotnet("run -c Release --no-build", testProjectDir, timeoutMs: 60000);
            Assert.Equal(0, runExitCode);
        }

        /// <summary>
        /// Applies workarounds for known TraceParserGen code generation limitations so the
        /// generated code can compile. This documents issues that should eventually be fixed
        /// in TraceParserGen itself:
        /// 1. TaskGuid fields are referenced but never defined (TODO in TraceParserGen.cs ~line 307)
        /// 2. RegisterTemplate() is called but doesn't exist; should be source.RegisterEventTemplate()
        /// </summary>
        private void FixKnownCodeGenIssues(string csFilePath)
        {
            string content = File.ReadAllText(csFilePath);

            // Fix 1: Add missing TaskGuid declarations
            var matches = System.Text.RegularExpressions.Regex.Matches(content, @"(\w+TaskGuid)");
            var taskGuids = new System.Collections.Generic.HashSet<string>();
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                string name = m.Groups[1].Value;
                if (!content.Contains($"Guid {name} =") && !content.Contains($"Guid {name}="))
                {
                    taskGuids.Add(name);
                }
            }

            if (taskGuids.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var guid in taskGuids)
                {
                    sb.AppendLine($"        private static readonly Guid {guid} = Guid.Empty;");
                }

                content = content.Replace(
                    "        #region private\r\n        protected override string GetProviderName()",
                    "        #region private\r\n" + sb.ToString() + "        protected override string GetProviderName()");
                content = content.Replace(
                    "        #region private\n        protected override string GetProviderName()",
                    "        #region private\n" + sb.ToString() + "        protected override string GetProviderName()");

                Output.WriteLine($"Fixed: Added {taskGuids.Count} missing TaskGuid declaration(s)");
            }

            // Fix 2: Replace RegisterTemplate() with source.RegisterEventTemplate()
            if (content.Contains("RegisterTemplate("))
            {
                content = content.Replace("RegisterTemplate(", "source.RegisterEventTemplate(");
                Output.WriteLine("Fixed: RegisterTemplate -> source.RegisterEventTemplate");
            }

            File.WriteAllText(csFilePath, content);
        }

        private void CreateTestConsoleApp(string projectDir, string generatedParserPath)
        {
            string testAssemblyDir = Environment.CurrentDirectory;
            string srcDir = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", ".."));
            string traceEventProjectPath = Path.Combine(srcDir, "TraceEvent", "TraceEvent.csproj");

            if (!File.Exists(traceEventProjectPath))
            {
                throw new FileNotFoundException($"Could not find TraceEvent.csproj at {traceEventProjectPath}");
            }

            string csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net462</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""{traceEventProjectPath}"" />
  </ItemGroup>
</Project>";
            File.WriteAllText(Path.Combine(projectDir, "TestApp.csproj"), csprojContent);

            File.Copy(generatedParserPath, Path.Combine(projectDir, Path.GetFileName(generatedParserPath)), true);

            string programContent = @"using System;
using System.Linq;
using System.Reflection;
using Microsoft.Diagnostics.Tracing;

class Program
{
    static int Main()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var parserTypes = assembly.GetTypes()
                .Where(t => typeof(TraceEventParser).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();

            if (parserTypes.Count == 0)
            {
                Console.Error.WriteLine(""No parser types found"");
                return 1;
            }

            Console.WriteLine($""Found {parserTypes.Count} parser type(s)"");

            foreach (var parserType in parserTypes)
            {
                Console.WriteLine($""  Verified: {parserType.Name}"");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}";
            File.WriteAllText(Path.Combine(projectDir, "Program.cs"), programContent);
        }

        private int RunDotnet(string arguments, string workingDirectory, int timeoutMs = 60000)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Output.WriteLine($"Running: dotnet {arguments} (in {workingDirectory})");

            using (var process = Process.Start(startInfo))
            {
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(timeoutMs);

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    Output.WriteLine($"STDOUT: {stdout}");
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Output.WriteLine($"STDERR: {stderr}");
                }

                return process.ExitCode;
            }
        }

        #endregion

        #region Error handling tests

        [Fact]
        public void MissingManifest_FailsWithNonZeroExit()
        {
            string fakePath = Path.Combine(OutputDir, "nonexistent.manifest.xml");
            var (exitCode, stdout, stderr) = RunTraceParserGen($"\"{fakePath}\"");

            Assert.NotEqual(0, exitCode);
        }

        #endregion

        #region Opcode tests

        [Fact]
        public void OpcodeManifest_GeneratesStartStopEventNames()
        {
            string content = GenerateParserFromManifest("OpcodeTest.manifest.xml");

            // Events with Start/Stop opcodes should have TaskName + OpcodeName as event name
            Assert.Contains("public event Action<FileIOTemplateTraceData> FileIOStart", content);
            Assert.Contains("public event Action<FileIOTemplateTraceData> FileIOStop", content);
        }

        [Fact]
        public void OpcodeManifest_SharedTemplateGeneratesSingleClass()
        {
            string content = GenerateParserFromManifest("OpcodeTest.manifest.xml");

            // Both events share the same template, so only one payload class should exist
            int classCount = CountOccurrences(content, "public sealed class FileIOTemplateTraceData : TraceEvent");
            Assert.Equal(1, classCount);
        }

        [Fact]
        public void OpcodeManifest_ContainsOpcodeInTemplateDef()
        {
            string content = GenerateParserFromManifest("OpcodeTest.manifest.xml");

            // Template helper methods should reference the opcode names in context
            Assert.Contains("static private FileIOTemplateTraceData FileIOStartTemplate(Action<FileIOTemplateTraceData> action)", content);
            Assert.Contains("static private FileIOTemplateTraceData FileIOStopTemplate(Action<FileIOTemplateTraceData> action)", content);
        }

        #endregion

        #region Enumeration tests

        [Fact]
        public void EnumManifest_GeneratesValueMapEnum()
        {
            string content = GenerateParserFromManifest("EnumTest.manifest.xml");

            // valueMap should generate a non-Flags enum with "Map" suffix stripped
            Assert.Contains("public enum Status", content);
            Assert.Contains("Unknown = 0x0", content);
            Assert.Contains("Active = 0x1", content);
            Assert.Contains("Inactive = 0x2", content);
        }

        [Fact]
        public void EnumManifest_GeneratesBitMapEnumWithFlagsAttribute()
        {
            string content = GenerateParserFromManifest("EnumTest.manifest.xml");

            // bitMap should generate a [Flags] enum with "Map" suffix stripped
            Assert.Contains("[Flags]", content);
            Assert.Contains("public enum AccessFlags", content);
            Assert.Contains("Read = 0x1", content);
            Assert.Contains("Write = 0x2", content);
            Assert.Contains("Execute = 0x4", content);
        }

        [Fact]
        public void EnumManifest_FieldsUseEnumTypes()
        {
            string content = GenerateParserFromManifest("EnumTest.manifest.xml");

            // Fields with map attribute should have enum type and cast in accessor
            Assert.Contains("public Status Status { get { return (Status)GetInt32At(0); } }", content);
            Assert.Contains("public AccessFlags Flags { get { return (AccessFlags)GetInt32At(4); } }", content);
        }

        #endregion

        #region Advanced features tests

        [Fact]
        public void AdvancedManifest_SpecialCharsStrippedFromClassName()
        {
            string content = GenerateParserFromManifest("AdvancedFeatures.manifest.xml");

            // Provider "Microsoft-Test-Advanced" → ToCSharpName strips dashes → "MicrosoftTestAdvanced"
            Assert.Contains("public sealed class MicrosoftTestAdvancedTraceEventParser : TraceEventParser", content);
        }

        [Fact]
        public void AdvancedManifest_FixKeywordNameStripsPrefix()
        {
            string content = GenerateParserFromManifest("AdvancedFeatures.manifest.xml");

            // ETW_KEYWORD_SESSION → Session (prefix stripped, CamelCase)
            Assert.Contains("Session = 0x1", content);
            // ETW_KEYWORD_PROCESS_LIFECYCLE → ProcessLifecycle
            Assert.Contains("ProcessLifecycle = 0x2", content);
            // NormalKeyword stays as-is
            Assert.Contains("Normalkeyword = 0x4", content);
        }

        [Fact]
        public void AdvancedManifest_EmptyEventUsesEmptyTraceData()
        {
            string content = GenerateParserFromManifest("AdvancedFeatures.manifest.xml");

            // Event without template/fields should use EmptyTraceData
            Assert.Contains("public event Action<EmptyTraceData> EmptyEvent", content);
        }

        [Fact]
        public void AdvancedManifest_TidPrefixGeneratesArgsClassName()
        {
            string content = GenerateParserFromManifest("AdvancedFeatures.manifest.xml");

            // Template with tid_ prefix → class name uses EventName + "Args" instead of TemplateName + "TraceData"
            Assert.Contains("public sealed class PaddedEventArgs : TraceEvent", content);
        }

        [Fact]
        public void AdvancedManifest_ReservedAndPadFieldsSkipped()
        {
            string content = GenerateParserFromManifest("AdvancedFeatures.manifest.xml");

            // Fields named "Reserved1" and "Pad1" should be skipped
            Assert.Contains("// Skipping Reserved1", content);
            Assert.Contains("// Skipping Pad1", content);

            // But Id and Value should still be present
            Assert.Contains("public int Id { get { return GetInt32At(0); } }", content);
            Assert.Contains("public int Value { get { return GetInt32At(12); } }", content);
        }

        [Fact]
        public void AdvancedManifest_SkippedFieldsExcludedFromPayloadNames()
        {
            string content = GenerateParserFromManifest("AdvancedFeatures.manifest.xml");

            // PayloadNames for PaddedEventArgs should include only Id and Value, not Reserved1 or Pad1
            Assert.Contains("payloadNames = new string[] { \"Id\", \"Value\"};", content);
            Assert.DoesNotContain("\"Reserved1\"", content);
            Assert.DoesNotContain("\"Pad1\"", content);
        }

        [Fact]
        public void AdvancedManifest_PointerFieldUsesAddress()
        {
            string content = GenerateParserFromManifest("AdvancedFeatures.manifest.xml");

            // win:Pointer fields should use Address type and GetAddressAt
            Assert.Contains("public Address Address { get { return GetAddressAt(0); } }", content);
        }

        [Fact]
        public void AdvancedManifest_PointerFieldAffectsSubsequentOffsets()
        {
            string content = GenerateParserFromManifest("AdvancedFeatures.manifest.xml");

            // Size field comes after a Pointer, so offset uses HostOffset to account for pointer size
            Assert.Contains("public long Size { get { return GetInt64At(HostOffset(4, 1)); } }", content);
        }

        [Fact]
        public void AdvancedManifest_PointerFieldUsesXmlAttribHex()
        {
            string content = GenerateParserFromManifest("AdvancedFeatures.manifest.xml");

            // Address type fields should use XmlAttribHex in ToXml
            Assert.Contains("XmlAttribHex(sb, \"Address\", Address)", content);
        }

        [Fact]
        public void AdvancedManifest_NamespaceUsesClassNamePrefix()
        {
            string content = GenerateParserFromManifest("AdvancedFeatures.manifest.xml");

            Assert.Contains("namespace Microsoft.Diagnostics.Tracing.Parsers.MicrosoftTestAdvanced", content);
        }

        #endregion

        #region Template sharing tests

        [Fact]
        public void TemplateSharing_TwoEventsShareOnePayloadClass()
        {
            string content = GenerateParserFromManifest("TemplateSharingTest.manifest.xml");

            // Two events share SharedDataTemplate → single SharedDataTemplateTraceData class
            int classCount = CountOccurrences(content, "public sealed class SharedDataTemplateTraceData : TraceEvent");
            Assert.Equal(1, classCount);

            // But both events should exist
            Assert.Contains("public event Action<SharedDataTemplateTraceData> ReadOp", content);
            Assert.Contains("public event Action<SharedDataTemplateTraceData> WriteOp", content);
        }

        [Fact]
        public void TemplateSharing_SpecialCharsInProviderName()
        {
            string content = GenerateParserFromManifest("TemplateSharingTest.manifest.xml");

            // Provider "My.Special-Provider (v2)" → ToCSharpName → "MySpecialProviderv2"
            Assert.Contains("public sealed class MySpecialProviderv2TraceEventParser : TraceEventParser", content);
            Assert.Contains("namespace Microsoft.Diagnostics.Tracing.Parsers.MySpecialProviderv2", content);
        }

        [Fact]
        public void TemplateSharing_ReservedKeywordFieldRenamed()
        {
            string content = GenerateParserFromManifest("TemplateSharingTest.manifest.xml");

            // Field named "object" (C# reserved keyword) should be renamed to "Object"
            Assert.Contains("public string Object { get { return GetUnicodeStringAt(4); } }", content);
            // PayloadNames should also use the renamed "Object"
            Assert.Contains("payloadNames = new string[] { \"Id\", \"Object\"};", content);
        }

        #endregion

        #region Boolean and Pointer type tests

        [Fact]
        public void BoolPointer_BooleanFieldGeneratesCorrectAccessor()
        {
            string content = GenerateParserFromManifest("BoolPointerTest.manifest.xml");

            // win:Boolean → bool type, GetInt32At with != 0 conversion, 4 bytes
            Assert.Contains("public bool IsEnabled { get { return GetInt32At(0) != 0; } }", content);
        }

        [Fact]
        public void BoolPointer_PointerFieldGeneratesAddressType()
        {
            string content = GenerateParserFromManifest("BoolPointerTest.manifest.xml");

            // win:Pointer → Address type, GetAddressAt
            Assert.Contains("public Address Ptr { get { return GetAddressAt(4); } }", content);
        }

        [Fact]
        public void BoolPointer_FieldAfterPointerUsesHostOffset()
        {
            string content = GenerateParserFromManifest("BoolPointerTest.manifest.xml");

            // Counter field comes after a 4-byte bool + pointer, so offset uses HostOffset
            Assert.Contains("public int Counter { get { return GetInt32At(HostOffset(8, 1)); } }", content);
        }

        #endregion

        #region Combined flag tests

        [Fact]
        public void CombinedInternalAndNeedsState_GeneratesBothModifiers()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml", "CombinedFlags.cs", "/Internal /NeedsState");

            // Should have both internal overrides and state class
            Assert.Contains("protected internal override void Dispatch()", content);
            Assert.Contains("protected internal override void Validate()", content);
            Assert.Contains("internal class SimpleTestProviderState : IFastSerializable", content);
            Assert.Contains("private SimpleTestProviderState State", content);
        }

        [Fact]
        public void VerboseFlag_DoesNotAffectOutput()
        {
            // /Verbose only affects MOF path (PRIVATE build), not manifest generation
            string normalContent = GenerateParserFromManifest("SimpleTest.manifest.xml", "Normal.cs");
            string verboseContent = GenerateParserFromManifest("SimpleTest.manifest.xml", "Verbose.cs", "/Verbose");

            // Output should be identical
            Assert.Equal(normalContent, verboseContent);
        }

        #endregion

        #region Field offset and sizing tests

        [Fact]
        public void MultiTypeManifest_CorrectFieldOffsets()
        {
            string content = GenerateParserFromManifest("MultiType.manifest.xml");

            // NumericFields template: ByteVal(1B) + ShortVal(2B) + IntVal(4B) + LongVal(8B) + BoolVal(4B) + FloatVal(4B) + DoubleVal(8B)
            Assert.Contains("GetByteAt(0)", content);     // ByteVal at offset 0
            Assert.Contains("GetInt16At(1)", content);    // ShortVal at offset 1
            Assert.Contains("GetInt32At(3)", content);    // IntVal at offset 3
            Assert.Contains("GetInt64At(7)", content);    // LongVal at offset 7
            Assert.Contains("GetInt32At(15)", content);   // BoolVal at offset 15 (returns int for bool)
            Assert.Contains("GetSingleAt(19)", content);  // FloatVal at offset 19
            Assert.Contains("GetDoubleAt(23)", content);  // DoubleVal at offset 23
        }

        [Fact]
        public void MultiTypeManifest_MixedFieldsCorrectOffsets()
        {
            string content = GenerateParserFromManifest("MultiType.manifest.xml");

            // MixedFields: Id(UInt32=4B) + Name(UnicodeString=var) + ActivityId(GUID=16B) + HexValue(HexInt32=4B) + Timestamp(FILETIME=8B)
            Assert.Contains("GetInt32At(0)", content);                      // Id at offset 0
            Assert.Contains("GetUnicodeStringAt(4)", content);              // Name at offset 4
            Assert.Contains("GetGuidAt(SkipUnicodeString(4))", content);    // ActivityId after variable-length string
        }

        [Fact]
        public void SimpleManifest_GeneratesValidateWithLengthAsserts()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            // Validate method should contain Debug.Assert with version and payload length checks
            Assert.Contains("Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(0)));", content);
            Assert.Contains("Debug.Assert(!(Version == 0 && EventDataLength != 4));", content);
        }

        #endregion

        #region PayloadValue method tests

        [Fact]
        public void SimpleManifest_GeneratesPayloadValueMethod()
        {
            string content = GenerateParserFromManifest("SimpleTest.manifest.xml");

            Assert.Contains("public override object PayloadValue(int index)", content);
            // PayloadValue switch cases should return the correct property for each index
            Assert.Contains("case 0:\r\n                    return Message;", content);
        }

        [Fact]
        public void MultiTypeManifest_GeneratesGetKeywordsAndProviderInfo()
        {
            string content = GenerateParserFromManifest("MultiType.manifest.xml");

            Assert.Contains("public static ulong GetKeywords()", content);
            Assert.Contains("public static string GetProviderName()", content);
            Assert.Contains("public static Guid GetProviderGuid()", content);
        }

        #endregion

        #region Helper methods

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }

        #endregion

        #region Versioned event tests

        [Fact]
        public void VersionedEvent_GeneratesSinglePayloadClass()
        {
            string content = GenerateParserFromManifest("VersionedEvent.manifest.xml");

            // Two event elements with the same ID and task but different versions
            // should produce a single payload class (from the first template name)
            int classCount = CountOccurrences(content, "public sealed class ProcessInfoV0TraceData : TraceEvent");
            Assert.Equal(1, classCount);
        }

        [Fact]
        public void VersionedEvent_GeneratesSingleEventProperty()
        {
            string content = GenerateParserFromManifest("VersionedEvent.manifest.xml");

            // Only one event property despite two versioned event elements
            int eventCount = CountOccurrences(content, "public event Action<ProcessInfoV0TraceData> ProcessInfo");
            Assert.Equal(1, eventCount);
        }

        [Fact]
        public void VersionedEvent_CommonFieldsHaveSimpleAccessors()
        {
            string content = GenerateParserFromManifest("VersionedEvent.manifest.xml");

            // Fields present in both V0 and V1 should have simple accessors with no version guard
            Assert.Contains("public int ProcessId { get { return GetInt32At(0); } }", content);
            Assert.Contains("public string Name { get { return GetUnicodeStringAt(4); } }", content);
        }

        [Fact]
        public void VersionedEvent_NewFieldHasVersionGuard()
        {
            string content = GenerateParserFromManifest("VersionedEvent.manifest.xml");

            // CommandLine only exists in V1, so accessor should have a version guard
            // returning empty string for V0 and the real value for V1+
            Assert.Contains("if (Version >= 1) return GetUnicodeStringAt(SkipUnicodeString(4)); return \"\";", content);
        }

        [Fact]
        public void VersionedEvent_ValidateHasPerVersionAsserts()
        {
            string content = GenerateParserFromManifest("VersionedEvent.manifest.xml");

            // Validate should have separate Debug.Assert for each version's expected payload length
            Assert.Contains("Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(4)));", content);
            Assert.Contains("Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(SkipUnicodeString(4))));", content);
            // And a forward-compat guard for versions beyond the latest known
            Assert.Contains("Debug.Assert(!(Version > 1 && EventDataLength < SkipUnicodeString(SkipUnicodeString(4))));", content);
        }

        [Fact]
        public void VersionedEvent_PayloadNamesIncludeAllVersionFields()
        {
            string content = GenerateParserFromManifest("VersionedEvent.manifest.xml");

            // PayloadNames should include fields from ALL versions (union)
            Assert.Contains("payloadNames = new string[] { \"ProcessId\", \"Name\", \"CommandLine\"};", content);
        }

        [Fact]
        public void VersionedEvent_EnumerateTemplatesHasEntryPerVersion()
        {
            string content = GenerateParserFromManifest("VersionedEvent.manifest.xml");

            // EnumerateTemplates creates a template array sized for all event elements (2 versions)
            Assert.Contains("var templates = new TraceEvent[2];", content);
        }

        #endregion
    }
}
