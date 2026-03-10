# TraceParserGen.Tests

This project contains automated tests for the TraceParserGen tool, which generates C# parser classes from ETW manifests.

## Overview

TraceParserGen.Tests validates the TraceParserGen code generation pipeline through 60 test cases covering:

1. **Simple manifest content validation** - Class names, namespaces, provider GUIDs, constructors, usings, keywords, events, payload accessors, Dispatch/Validate/ToXml/EnumerateTemplates generation
2. **Multi-type data field tests** - UInt8, UInt16, Int32, Int64, Float, Double, Boolean, GUID, FILETIME, UnicodeString, AnsiString, HexInt32 field accessors and correct byte offsets
3. **Opcode tests** - Events with win:Start/win:Stop opcodes, TaskName+OpcodeName event naming, shared template deduplication
4. **Enumeration tests** - valueMap (C# enum), bitMap ([Flags] enum), enum-typed field accessors with cast expressions
5. **Advanced feature tests** - Special character stripping (ToCSharpName), ETW_KEYWORD_ prefix normalization (FixKeywordName), empty events (EmptyTraceData), tid_ prefix template naming, Reserved/Pad field skipping, win:Pointer/Address type with HostOffset, XmlAttribHex for Address fields
6. **Template sharing tests** - Multiple events sharing one payload class, reserved C# keyword field renaming (object → Object)
7. **Boolean and Pointer type tests** - win:Boolean → `GetInt32At() != 0`, win:Pointer → Address/GetAddressAt, HostOffset for subsequent fields
8. **Qualifier flag tests** - `/Internal`, `/NeedsState`, combined `/Internal /NeedsState`, `/Verbose`
9. **Output file tests** - Default .cs extension, explicit output path
10. **Build integration test** - Generate, compile, and instantiate parser in a real application
11. **Error handling tests** - Missing manifest
12. **Field offset and sizing tests** - Correct byte offset computation for sequential fixed-size and variable-size fields
13. **PayloadValue and static helper tests** - PayloadValue(index), GetKeywords(), GetProviderName(), GetProviderGuid()

## Test Files

- **ParserGenerationTests.cs**: Main test class with 60 test cases across 14 categories
- **TestBase.cs**: Base class providing TraceParserGen execution helpers and temp directory management
- **inputs/SimpleTest.manifest.xml**: Two tasks, UnicodeString and Int32 fields, 1 keyword
- **inputs/MultiType.manifest.xml**: 12+ field types across 3 events and 3 keywords
- **inputs/OpcodeTest.manifest.xml**: Events with win:Start/win:Stop opcodes sharing a template
- **inputs/EnumTest.manifest.xml**: valueMap and bitMap with enum-typed fields
- **inputs/AdvancedFeatures.manifest.xml**: Provider with dashes, ETW_KEYWORD_ keywords, empty events, tid_ prefix templates, Reserved/Pad fields, Pointer type
- **inputs/TemplateSharingTest.manifest.xml**: Two events sharing one template, special characters in provider name, reserved keyword field name
- **inputs/BoolPointerTest.manifest.xml**: Boolean and Pointer fields with HostOffset offset computation

## Running Tests

```bash
dotnet test src/TraceParserGen.Tests/TraceParserGen.Tests.csproj -c Release
```
