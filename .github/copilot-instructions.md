# Copilot Instructions for PerfView

## Project Overview

PerfView is a Windows performance-analysis tool for investigating CPU and memory issues, built on the TraceEvent library for parsing ETW and EventPipe trace data. The solution (`PerfView.sln`) contains the WPF GUI application, the cross-platform TraceEvent library, and several supporting libraries.

## Architecture

- `src/PerfView/` — WPF GUI application (Windows-only, .NET Framework 4.7.2+, C# 7.3 features only)
- `src/TraceEvent/` — Core trace parsing library (cross-platform, targets netstandard2.0)
- `src/FastSerialization/` — Lightweight binary serialization library
- `src/MemoryGraph/` — Memory dump analysis (graph-based heap representation)
- `src/Utilities/` — Shared utility code
- `src/HeapDump*/` — Heap dump capture using ClrMD (Windows-only native interop)
- `src/EtwClrProfiler/` — Native C++ CLR profiler emitting ETW events (Windows-only)
- `src/PerfViewExtensions/` — Extensibility mechanism ("Global" project)

## Build & Test

- **Full solution (Windows):** `build.cmd` or open `PerfView.sln` in Visual Studio 2022+ and build.
- **Individual projects (cross-platform via dotnet CLI):**
  - `dotnet build src/TraceEvent/TraceEvent.csproj -c Debug`
  - `dotnet build src/FastSerialization/FastSerialization.csproj -c Debug`
  - `dotnet build src/MemoryGraph/MemoryGraph.csproj -c Debug`
- **Running tests:** `dotnet test <TestProject>.csproj -c Debug` — always use Debug configuration so assertions are active.
  - `src/TraceEvent/TraceEvent.Tests/TraceEvent.Tests.csproj`
  - `src/FastSerialization.Tests/FastSerialization.Tests.csproj`
  - `src/LinuxTracing.Tests/LinuxTracing.Tests.csproj`
  - `src/PerfView.Tests/PerfView.Tests.csproj` (Windows-only)
  - `src/SymbolsAuth.Tests/SymbolsAuth.Tests.csproj`
- Tests use **xUnit**. The full test suite should complete in under 1 minute.
- NuGet uses central package management (`src/Directory.Packages.props`). Use the repo-local `Nuget.config` when restoring: `dotnet restore --configfile Nuget.config`.

## C# Coding Conventions

Follow existing patterns — when in doubt, match the surrounding code.

### Naming
- Standard .NET conventions: `PascalCase` for types, methods, and properties; `camelCase` for parameters and locals.
- Private instance fields: prefix with `m_` (e.g., `m_nodeCount`). The `_` prefix is also acceptable.
- Static fields: prefix with `s_` (e.g., `s_defaultSize`).
- No Hungarian notation.

### Class Layout
- Order members for readability as a **public contract**: constructors/factories first, then properties, then methods.
- All private members go **after** all public members, wrapped in `#region private` so Visual Studio outlining (Ctrl-M Ctrl-O) collapses them.
- Fields go **together at the end** of the private region — this makes it easy to see all object state at a glance.

### Comments & Documentation
- This codebase is **heavily commented** — maintain that standard.
- Public types and public members exposed outside their assembly **must** have XML doc comments (`/// <summary>`). Parameter-level docs are optional if names are descriptive.
- Private fields often need comments, especially to document invariants they maintain.
- Use inline comments to explain non-obvious logic and design decisions.

### Error Handling & Assertions
- Use `Debug.Assert()` liberally to validate internal invariants — this is why tests must run in Debug configuration.
- Throw specific exceptions with descriptive messages for public API misuse.

### Other Patterns
- **Type aliases** for semantic clarity: `using Address = System.UInt64;`
- **Lazy initialization** for expensive sub-objects (check for null, create on first access).
- **Reuse event objects** in TraceEvent callbacks to minimize GC pressure.

## Making Changes

- Keep changes **minimal and focused** — complexity is the enemy (see `CONTRIBUTING.md`).
- Prefer **simplicity over cleverness**. Performance optimizations that add complexity need measurements to justify them.
- Run tests in **Debug** configuration before submitting changes.
- PerfView embeds its support DLLs into the EXE at build time, creating non-obvious build dependencies. If you see "DLL not found" errors, a normal (non-clean) rebuild usually fixes it.
- The `Global` project depends on PerfView — expect unresolved references there until PerfView builds first.
