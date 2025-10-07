# TraceParserGen.Tests

This project contains automated tests for the TraceParserGen tool, which generates C# parser classes from ETW manifests or EventSource implementations.

## Overview

TraceParserGen.Tests implements a comprehensive test framework that validates the entire code generation pipeline:

1. **Run TraceParserGen.exe** with test input (manifest file or EventSource DLL)
2. **Verify successful generation** of C# parser files
3. **Create a temporary console project** that references TraceEvent and includes the generated parser
4. **Build the temporary project** to ensure the generated code compiles
5. **Run the test application** to verify no runtime errors or assertions occur

## Test Structure

### Test Files

- **ParserGenerationTests.cs**: Main test class containing test cases for parser generation
- **TestBase.cs**: Base class providing common test infrastructure and helper methods
- **inputs/**: Directory containing test input files (manifests, sample DLLs)

### Sample Test

The `CanGenerateParserFromManifest` test demonstrates the full pipeline:
- Uses a simple ETW manifest (`SimpleTest.manifest.xml`) as input
- Generates a parser class from the manifest
- Creates a temporary console app that uses reflection to find and instantiate the parser
- Builds and runs the console app to ensure everything works

## Requirements

- **Windows**: Tests require Windows with .NET Framework support to run TraceParserGen.exe
- **TraceParserGen**: The TraceParserGen project must be built before running tests
- **TraceEvent**: The TraceEvent project must be built before running tests

## Running Tests

```bash
# Build dependencies first
dotnet build src/TraceParserGen/TraceParserGen.csproj -c Release
dotnet build src/TraceEvent/TraceEvent.csproj -c Release

# Run tests
dotnet test src/TraceParserGen.Tests/TraceParserGen.Tests.csproj -c Release
```

## Adding New Tests

To add a new test case:

1. Add your test input file (manifest or DLL) to the `inputs/` directory
2. Create a new test method in `ParserGenerationTests.cs`
3. Follow the pattern of the existing `CanGenerateParserFromManifest` test:
   - Call `RunTraceParserGen()` to generate the parser
   - Call `CreateTestConsoleApp()` to create a test application
   - Call `BuildTestApp()` to build the test application
   - Call `RunTestApp()` to verify the generated code works

## Test Console Application

The test creates a temporary console application that:
- References the Microsoft.Diagnostics.Tracing.TraceEvent library
- Includes the generated parser C# file
- Uses reflection to discover all TraceEventParser-derived types
- Verifies the parsers can be instantiated and have the expected methods

This approach allows us to test that the generated code:
- Compiles successfully
- Contains valid C# syntax
- Implements the expected TraceEventParser interface
- Can be used in a real application

## Platform Notes

Tests are designed to run on Windows where TraceParserGen.exe (a .NET Framework application) can run natively. On non-Windows platforms, tests will skip with an informational message.

## Future Enhancements

Potential improvements to the test framework:
- Add tests for EventSource-based parser generation (using DLLs as input)
- Add tests for complex manifest scenarios (multiple providers, complex templates)
- Add validation of generated parser output against expected baselines
- Add performance benchmarks for parser generation
- Add tests that actually parse ETL files with the generated parsers
