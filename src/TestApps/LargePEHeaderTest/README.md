# Large PE Header Test

This console application demonstrates the improvement in the new PEFile implementation when handling PE files with headers larger than 1024 bytes.

## Purpose

The original PEFile implementation had validation that would fail when PE file headers exceeded 1024 bytes. This could happen with executables that have many sections (e.g., heavily optimized binaries, files with many resources, or specially crafted test files).

The new ReadOnlySpan-based implementation uses a progressive read pattern that:
1. Initially reads 1024 bytes
2. Calculates the actual required header size
3. Re-reads with the correct size if needed
4. Provides automatic bounds checking via ReadOnlySpan

## Building

```bash
cd src/TestApps/LargePEHeaderTest
dotnet build
```

## Running

```bash
dotnet run
```

This will:
1. Generate a PE file named `LargeHeaderTest.exe` with 20 sections
2. Display the header size (should be > 1024 bytes)
3. Analyze the file to show its structure

## Expected Output

```
=== Large PE Header Test Generator ===
This tool generates a PE file with headers larger than 1024 bytes
to demonstrate the improvement in the new PEFile implementation.

Header size: 1168 bytes (Original implementation limited to 1024 bytes)

Generated test PE file: LargeHeaderTest.exe
File size: 4608 bytes

=== PE File Analysis ===
Total file size: 4608 bytes
PE header offset: 128 bytes
Machine type: 0x8664
Number of sections: 20
Optional header size: 240 bytes
Sections start at: 392 bytes
Total header size: 1192 bytes

✓ Headers are 1192 bytes (> 1024 bytes)
  This would FAIL with the original PEFile implementation
  This SUCCEEDS with the new ReadOnlySpan-based implementation
```

## Testing with TraceEvent

You can test the generated file with the TraceEvent PEFile class:

```csharp
using PEFile;

var peFile = new PEFile("LargeHeaderTest.exe");
Console.WriteLine($"Machine: {peFile.Header.Machine}");
Console.WriteLine($"Sections: {peFile.Header.NumberOfSections}");
Console.WriteLine($"Header Size: {peFile.Header.PEHeaderSize}");
```

### Original Implementation
Would throw an exception or fail validation when headers exceed 1024 bytes.

### New Implementation
Handles files with headers of any size correctly by using progressive reads.

## Key Differences

| Aspect | Original | New (ReadOnlySpan) |
|--------|----------|-------------------|
| Initial buffer | 1024 bytes fixed | 1024 bytes |
| Validation | Required all headers in buffer | Validates only what's read |
| Re-reading | Would fail if > 1024 | Re-reads with correct size |
| Safety | Manual pointer bounds | Automatic span bounds |
| Large headers | ❌ Fails | ✅ Works |

## Related Issue

This test addresses issue #2316 where PE files with many sections failed to load due to the 1024-byte limitation.
