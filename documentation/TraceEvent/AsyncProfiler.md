# Async-profiler architecture

This document explains how TraceEvent uses async-profiler events to show
the async callers of sampled code. It covers both classic state-machine
async (V1) and runtime-supported async (V2), and traces that contain both.

## The problem

When an async method awaits something that is not yet complete, it returns
to its caller and its thread moves on to other work. When the awaited
operation finishes, the method resumes later, often on a different thread,
called by runtime dispatch code rather than by its original caller.

A CPU sample taken after the resume therefore shows the runtime dispatcher
at the bottom of the stack. The async methods that are waiting for the
resumed method's result are no longer on the stack:

```text
Physical stack at the sample (caller to callee):
  Thread -> Runtime dispatch -> ReadResponse -> ParseResponse
```

The async profiler in the .NET runtime records, each time async code
resumes on a thread, the chain of async methods waiting on it. TraceEvent
combines that recorded chain with the physical stack:

```text
Recorded async chain:
  HandleRequest -> FetchDocument -> ReadResponse

Reconstructed stack:
  Thread -> HandleRequest -> FetchDocument -> ReadResponse -> ParseResponse
```

- `ParseResponse` is sampled work from the physical stack.
- `ReadResponse` appears in both inputs and is shown once.
- `HandleRequest` and `FetchDocument` are added from the recorded chain.

The added callers are waiting, not executing. Their inclusive time in a
stack view means "this work was done on behalf of this caller", not
additional CPU time. Sample counts, timestamps, and weights never change.

## Key terms

| Term | Meaning |
|---|---|
| V1 | Classic compiler-generated state-machine async (`StateMachineAsync`). |
| V2 | Runtime-supported async (`RuntimeAsync`). |
| Physical stack | The frames actually on the thread's stack when a sample was taken. |
| Async call stack | One recorded chain of waiting async methods. It becomes active when async code resumes on a thread and ends when that code suspends again or completes. |
| Dispatcher | The runtime code that resumes async work. A dispatcher can resume many times; each resume produces a separate async call stack. |
| Continuation wrapper | A V2 runtime frame through which resumed code is entered. It marks where the physical and recorded stacks join. |
| Stitching | Combining a physical stack with the active async call stacks into one stack. |
| QPC | The high-resolution timestamp used by the trace. Async events and samples share the same QPC clock. |
| ETLX | TraceEvent's processed trace cache, created when an ETL or EventPipe trace is opened with `TraceLog`. |

## How it works

```text
ETW or EventPipe trace
        |
        v
1. Decode     async-profiler event batches into individual records
        |
        v
2. Index      when each async call stack was active, per thread
        |
        v
3. Persist    the index and its symbol references in the ETLX file
        |
        v
4. Stitch     a physical stack with the async call stacks active at its time
        |
        v
5. Present    optional cleanup and labels, then emit to a stack source
```

Steps 1 to 3 run once, while the trace is converted to ETLX, whether or
not anyone later asks for async stacks. Steps 4 and 5 run on demand, when a
stack source is generated. The ETLX file stores the evidence, not
pre-stitched samples, so different presentation settings can be applied
later without reconverting the trace.

## 1. Decoding events

The runtime's `System.Runtime.CompilerServices.AsyncProfilerEventSource`
emits a single `AsyncEvents` event. Its payload is a batch: a header
identifying the thread, followed by many small records such as "resumed
with this call stack", "method completed", or "suspended". Each record
carries its own timestamp, so TraceEvent uses that time, not the time the
batch was written.

The records are delivered to the index builder through callbacks; they are
not exposed as separate `TraceEvent` objects.

**Forward compatibility.** When profiling starts or its configuration
changes, the runtime sends a metadata record that lists, for each record
type, its schema version and how its payload length is encoded. TraceEvent
keeps this list per process, because metadata does not appear in every
batch. With it, the decoder can:

- read the fields it understands from a known record type and skip any
  fields added by a newer runtime, and
- skip record types it does not know at all.

**Errors.** A malformed record is skipped using its declared length. A
damaged batch header stops decoding of that batch. The index builder then
decides which call-stack state can still be trusted (see below).

**Not supported yet.** A call-stack record can refer to a previously sent
call stack instead of including its frames. The decoder recognizes this
form, but the runtime does not emit it yet, and the index builder does not
look up the referenced frames.

## 2. Building the index

The index builder turns the decoded records into a timeline per thread:
which async call stacks were active, and when.

**Starting point.** For each thread, the builder waits for metadata and
then a reset record before it trusts anything. A reset with a timestamp
earlier than the first metadata is ignored, even if it arrives later,
because it may be left over from a previous profiling session.

**Main rules.**

| Record | Effect |
|---|---|
| Resume (with call stack) | Starts a new async call stack on the thread. If one is already active, the new one is nested inside it. |
| V1 append (with more frames) | Adds frames to the innermost active call stack. |
| Suspend or complete | Ends the innermost active call stack at that timestamp. |
| Method completed, exception unwind, wrapper reset | Recorded as progress of the innermost call stack (used in step 4). |
| Context creation | Recorded as a parent/child relationship between dispatchers. |
| Thread reset | Ends all active call stacks on the thread and starts fresh. |
| End of trace | Ends any still-active call stacks with an open end time. |

A call stack is active from its start time up to, but not including, its
end time. Nested call stacks overlap their outer call stack in time; the
outer one is not split into pieces.

**Recovery.** If decoding fails for a thread, the builder ends that
thread's active call stacks at the failure time and ignores it until the
next reset. If metadata is damaged, all threads of that process wait for
new metadata. Call stacks already recorded, and other processes, are not
affected. Untrusted periods remain gaps; they are never filled with a
nearby call stack.

## 3. Symbols and persistence

**Frame identity.** Both V1 and V2 record native instruction pointers
(IPs) for each frame. V2 IPs come from each method's continuation. A V1 IP
points to the start of the state machine's `MoveNext` method, and V1 also
records the state machine's current compiler state, which identifies the
`await` the method is suspended at. The compiler state is not an IL or
native offset, and it is not used as a completion counter.

TraceEvent does not map a V1 compiler state to an IL offset or source
line. A recorded V1 frame therefore resolves to its method, but not to the
`await` it is suspended at. A V1 method that is running when sampled is the
exception, because its physical frame provides the exact IP.

**Symbols.** As TraceLog processes the trace, it registers every recorded
IP, so later method-load and rundown events can resolve it to a method.
Call stacks reference TraceLog code addresses instead of copying method
names. A registered address is not guaranteed to resolve to a method; the
frame is kept either way.

Physical and recorded frames are matched by method identity, not by
display name or raw method index. The same method can have several native
code versions, for example because of tiered compilation.

**Storage.** Identical frame lists are stored once and shared. Thread state
is keyed by process instance and OS thread ID, because OS process IDs can
be reused within a trace.

The index is stored in its own section of the ETLX file and loaded only
when an async query needs it. Its format has its own version, separate
from the runtime's event format. Raw `AsyncEvents` batches are not kept in
the ETLX file unless `TraceLogOptions.KeepAllEvents` is set; the index
contains everything needed for stitching.

## 4. Stitching

For each sample, the stitcher finds the async call stacks active on the
sample's thread at the sample's timestamp. It then walks the physical
stack and the recorded call stacks together, from leaf to root, and joins
them at verified handoff points. It keeps everything the physical stack
shows and adds only the waiting callers that are missing.

**Why progress matters.** An async call stack is recorded when async code
resumes. By the time of a later sample, some of the recorded methods may
already have completed. The stitcher must skip those, or completed methods
would appear as callers. It uses the progress recorded in step 2:

| Kind | With method-completion events | Without them |
|---|---|---|
| V2 | Count of completed methods plus exception unwinds. | Position of the current continuation wrapper. This already includes exceptions. |
| V1 | Count of completed methods plus exception unwinds, checked against the physical stack. | Matching of physical state-machine frames, adjusted for exception unwinds. |

A V1 method can be logically complete while its frame is still on the
physical stack, returning. The stitcher accounts for this so the same
progress is not counted twice. When the evidence is ambiguous, for
example with recursion, the physical frames are kept as they are.

Method-completion events are optional in the runtime's configuration. The
stitcher uses them only if they were observed for the same process, kind,
and profiling configuration.

**Join points.** V2 normally joins at a continuation wrapper. V1 joins at a
recognized dispatcher or state-machine box. Recognizing runtime code alone
is not enough to join; the frames around it must match too. A join point
can identify which method is about to run without proving it has started;
such a frame is labeled as a dispatch target, not as executing.

For V2 there is also a fallback for stacks without a wrapper. It is used
only when the evidence is unambiguous: exactly one active call stack that
is the most recent on the thread, completion events available, and no
conflicting physical frames.

**What is not invented.** If the physical stack is truncated and cannot be
matched, its missing root is shown as `BROKEN`. If no async call stack is
active, or there is no physical stack, the sample is produced exactly as
without async support. Samples are never dropped to hide missing data.

## 5. Presentation

Stitching produces a structurally correct stack. Presentation then
decides how it is displayed. Every reconstructed frame keeps a role,
independent of its display name:

| Role | Meaning |
|---|---|
| Physical / current | Observed on the physical stack. |
| Async caller | From the recorded chain; waiting, not executing. |
| Dispatch target | The method about to be run by a dispatcher; not yet proven to execute. |
| Completed | A V1 method still on the physical stack but already logically complete. |
| Completion return | Return/unwind work after completion, attributed to the logical root. |
| Historical parent | The async context that created this one (optional). |
| Incomplete root | A marker for a missing root, such as `BROKEN`. |

These roles, the captured IP, and the V1 state are kept in the stack
source's frame metadata, even when two frames display the same name.

**Steps, in order.**

1. Structural stitching (always, when async stacks are enabled).
2. Historical parent contexts, if enabled.
3. Normalization of known runtime patterns (startup, reentrant dispatch,
   handoff, completion return), if enabled.
4. Infrastructure cleanup, if enabled.
5. Custom transforms, in registration order.
6. Output: frame names, annotations, and the host's grouping roots.

**Settings** (`AsyncCallStackTransforms`):

| Setting | Default | Effect |
|---|---|---|
| `EnableNormalization` | On | Simplifies verified runtime patterns. Turning it off does not turn off stitching. |
| `EnableInfrastructureCleanup` | Off | Groups verified async runtime frames. |
| `EnableFrameAnnotations` | Off | Adds V1/V2 kind and V1 state to frame names. |
| `EnableParentContexts` | Off | Adds the creating async context above each active one. |
| `MaximumParentContextDepth` | 1 | How many parent contexts to add. |

**Infrastructure cleanup** is not general framework filtering. It only
groups a contiguous run of runtime frames that belong to the same call
stack and the same runtime module, with nothing from the application in
between. If removing the run would make a callee look like it was called
directly by an application method, the run is replaced by an
`[Async dispatcher frames]` marker instead.

**Historical parents** show where an async operation was started, not
what the parent was doing at the sample time. If both completion and
exception-unwind events are available, the parent's frames are adjusted
to the time the child was created; otherwise its recorded call stack is
shown as is. Missing relationships are not inferred.

**Custom transforms** receive a callback context that is reused between
calls. Do not keep references to it, its call stacks, or its frames after
the callback returns. A transform can reorder, remove, or add display-only
frames, but it cannot mark a frame as physically executing. Exceptions
from a transform propagate to the caller.

## Runtime-specific rules

Names of runtime methods and types change between runtime versions, so
they are kept out of the shared stitching logic:

- `AsyncProfilerRuntimeNames` holds the runtime method and type names.
- `AsyncProfilerRuntimeSupport` classifies frames and matches runtime
  frame sequences.
- `AsyncProfilerRuntimeResolver` picks the rules for each process based on
  the CoreLib and runtime modules recorded in the trace, not the runtime
  running the analysis.

CoreCLR 11.0 and later use the CoreCLR 11 rules. Older, missing, or
conflicting version information uses a common, more conservative rule set.

Frames are treated as belonging to the same runtime module only when the
trace shows it: the same module index, a native image linked to its
managed module, or a matching full path and PDB signature. Equal module
names are not enough.

## Using it

The ETLX index is built automatically when a trace with async-profiler
events is converted. Stitching must be requested.

**With the existing thread-time computers.** Both
`ThreadTimeStackComputer` (ETW CPU and wait time) and
`SampleProfilerThreadTimeComputer` (EventPipe sample profiler) support it:

```csharp
var computer = new ThreadTimeStackComputer(traceLog, symbolReader);
computer.EnableAsyncCallStacks = true;
computer.AsyncCallStackTransforms.EnableFrameAnnotations = true; // optional

var stackSource = new MutableTraceEventStackSource(traceLog);
computer.GenerateThreadTimeStacks(stackSource);
```

`AsyncCallStacksActive` is true after generation if a usable index was
found. It does not mean every sample was stitched.

**Stitching individual stacks.** `AsyncProfilerCallStackSourceBuilder`
returns a stitched stack for an event or for a physical stack and
timestamp. It does not add samples; the caller does that. For ETW events,
pass the event itself so the builder uses the time the user-mode stack was
captured.

The builder runs the same pipeline as the thread-time computers, not just
structural stitching. Configure it through its `Transforms` property
before producing stacks:

```csharp
var output = new MutableTraceEventStackSource(traceLog);
var builder = new AsyncProfilerCallStackSourceBuilder(output);
builder.Transforms.EnableInfrastructureCleanup = true; // optional

StackSourceCallStackIndex stack = builder.GetCallStack(sampleEvent);
```

**Querying recorded call stacks.** `AsyncProfilerCallStackComputer`
returns the async call stacks active on a thread at a timestamp, ordered
innermost first. Query by event, or by OS process ID, OS thread ID, and
QPC timestamp. The frames are exactly as recorded, leaf to root: no
progress is applied and no stitching is done. Results stay valid after
later queries.

```csharp
var computer = new AsyncProfilerCallStackComputer(traceLog);
foreach (AsyncProfilerCallStack callStack in computer.GetCallStacks(sampleEvent))
{
    Console.WriteLine($"{callStack.Kind} depth {callStack.Depth}");
    foreach (AsyncProfilerCallStackFrame frame in callStack.CapturedFrames)
    {
        string name = frame.CodeAddress == CodeAddressIndex.Invalid
            ? $"0x{frame.CapturedInstructionPointer:x}"
            : traceLog.CodeAddresses.Name(frame.CodeAddress);
        Console.WriteLine("  " + name);
    }
}
```

**Exporting to Speedscope.** `SpeedScopeStackSourceWriter.WriteAsyncCallStacksAsJson`
writes the recorded async call stacks as a timeline, one profile per
thread, without CPU samples or physical stacks. A frame stays open while
its call stack is active and has not completed. Widths show elapsed time,
not CPU time. An overload that takes a sampling interval instead takes
snapshots on a fixed time grid.

```csharp
SpeedScopeStackSourceWriter.WriteAsyncCallStacksAsJson(traceLog, "async.speedscope.json");
```

Keep the `TraceLog` open while querying or resolving frames. The builder
and transforms are not thread-safe, and stack generation must not be
called reentrantly.

## Limitations

- Recorded V1 frames resolve to the method start, not the `await`
  location. Compiler states are not mapped to IL offsets or source lines.
- Stitching can only use what was captured. Lost events, sessions started
  late, or untrusted periods produce plain physical stacks, not guesses.
- References to previously sent call stacks (zero-frame call-stack
  records) are not resolved. The runtime does not emit them yet.
- A call stack still active at the end of the trace has an open end time;
  this does not mean the work continued after the trace.
- Observed completion events show the feature was enabled, not that every
  completion was captured.
- Historical parent contexts show where work was started, not the
  synchronous path in between.

## Implementation map

Implementation files are under `src\TraceEvent\AsyncProfiler`. Parser
types use the `Microsoft.Diagnostics.Tracing.Parsers` namespaces; query and
reconstruction types use `Microsoft.Diagnostics.Tracing.Computers`.

| Responsibility | Main implementation |
|---|---|
| Batch framing and record decoding | `AsyncProfilerTraceEventParser.cs`, `AsyncProfilerTypes.cs`, `AsyncProfilerBufferReader.cs` |
| Building the index and recovery | `AsyncProfilerIndexBuilder.cs` |
| Recorded call stacks and frames | `AsyncProfilerCallStack.cs`, `AsyncProfilerCallStackKind.cs`, `AsyncProfilerCallStackFrame.cs`, `AsyncProfilerCallStackFrames.cs`, `AsyncProfilerThreadKey.cs` |
| Index storage and time queries | `AsyncProfilerCallStacksIndex.cs` |
| Conversion, symbol binding, and ETLX storage | `src\TraceEvent\TraceLog.cs` |
| Public queries | `AsyncProfilerCallStackComputer.cs` |
| Public stack building and callback context | `AsyncProfilerCallStackSourceBuilder.cs`, `AsyncProfilerCallStackTransformContext.cs` |
| Stitching and join points | `AsyncProfilerCallStackStitcher.cs`, `AsyncProfilerCallStackStitchingState.cs`, `AsyncProfilerStitchedFrame.cs`, `AsyncProfilerStitchResult.cs`, `AsyncProfilerCallStackStitchBoundary.cs` |
| Method-name normalization | `AsyncProfilerMethodNames.cs` |
| Runtime-specific rules | `AsyncProfilerRuntimeNames.cs`, `AsyncProfilerRuntimeSupport.cs`, `AsyncProfilerRuntimeResolver.cs`, `AsyncProfilerRuntimeSequenceMatch.cs` |
| Historical parent contexts | `AsyncProfilerParentContextBuilder.cs` |
| Presentation settings and transforms | `AsyncCallStackTransforms.cs`, `AsyncProfilerCallStackTransformPipeline.cs`, `AsyncProfiler*TransformPass.cs`, `AsyncProfilerCallStackTransformHelpers.cs` |
| Stack-source output and frame metadata | `AsyncProfilerCallStackSourceBuilderCore.cs`, `AsyncProfilerCallStackDiagnostics.cs`, `src\TraceEvent\TraceEventStacks.cs` |
| Speedscope export | `src\TraceEvent\Stacks\SpeedScopeStackSourceWriter.cs` |
| Thread-time computers | `src\TraceEvent\Computers\SampleProfilerThreadTimeComputer.cs`, `src\TraceEvent\Computers\ThreadTimeComputer.cs` |
