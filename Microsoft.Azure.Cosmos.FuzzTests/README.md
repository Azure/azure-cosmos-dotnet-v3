# Azure Cosmos DB .NET SDK — Fuzz Tests

Coverage-guided fuzz testing for the Azure Cosmos DB .NET SDK using
[SharpFuzz](https://github.com/Metalnem/sharpfuzz) +
[libfuzzer-dotnet](https://github.com/Metalnem/libfuzzer-dotnet).

> Follows the official Microsoft guidance: [Fuzzing C# Code with SharpFuzz](https://eng.ms/docs/cloud-ai-platform/azure-core/core-compute-and-host/specialized-compute-arunki/acc-tests-and-tools/tooling/fuzzing/fuzzing_csharp)
> and [OneFuzz Docs — Fuzzing .NET Code](https://eng.ms/docs/cloud-ai-platform/microsoft-specialized-clouds-msc/msc-security/security-fundamentals/the-onefuzz-service/onefuzz/fuzzeronboarding/notwindows/fuzzing-dotnet-code).

## How It Works

Each fuzz target is a class with a `public static void Fuzz(ReadOnlySpan<byte> input)` method that:

1. Converts raw bytes to the parser's input type (string, stream, etc.)
2. Calls the SDK parser under test
3. Catches only exceptions the parser is **designed** to throw (e.g., `FormatException`)
4. Lets **unexpected** exceptions propagate — these are real bugs
5. Optionally validates invariants on success (e.g., round-trip consistency)

When run with `libfuzzer-dotnet`, the libFuzzer engine:

- Starts with seed inputs from `seeds/<target>/`
- Mutates them (bit flips, byte insertions, splicing, dictionary tokens)
- Tracks which code branches each input reaches (via SharpFuzz instrumentation)
- Keeps mutations that discover new branches, discards ones that don't
- Saves any crashing input to disk as `crash-<hash>` for later debugging

**No Cosmos DB endpoint or emulator is needed.** Tests run against in-memory SDK parser code.

---

## Quick Start

> **Recommended:** use the helper scripts in `scripts/` instead of running
> `libfuzzer-dotnet` by hand. They build, instrument, copy seeds to a fresh
> corpus, organize outputs under `.fuzz-runs/<timestamp>/<target>/`, and print
> a summary table.
>
> ```powershell
> # Fuzz one target for 5 minutes:
> .\scripts\Run-Fuzz.ps1 -Target JsonNavigatorFuzz -Seconds 300
>
> # Fuzz all 7 targets for 2 minutes each:
> .\scripts\Run-Fuzz.ps1 -Target all -Seconds 120
>
> # Fast PR check (seed validation only, no fuzzing):
> .\scripts\Run-Fuzz.ps1 -Mode validate -Target all
>
> # Reproduce a crash and get a real .NET stack trace:
> .\scripts\Repro-Crash.ps1 -Target JsonNavigatorFuzz `
>     -CrashFile .\.fuzz-runs\20251210-153012\JsonNavigatorFuzz\crashes\crash-abc123
> ```
>
> The manual steps below describe what those scripts do under the hood.

### Prerequisites

- **.NET SDK 8.0+** — verify with `dotnet --version`
- **Windows, Linux, or macOS** — libFuzzer mode works on all three
- **`SharpFuzz.CommandLine`** instrumentation tool (installed below)
- **`libfuzzer-dotnet`** binary — download `libfuzzer-dotnet-windows.exe` from
  [Metalnem/libfuzzer-dotnet releases](https://github.com/Metalnem/libfuzzer-dotnet/releases)
  and place it on your `PATH` (e.g., `C:\Users\<you>\.dotnet\tools\`)

### 1. Install the SharpFuzz instrumentation tool

```powershell
dotnet tool install --global SharpFuzz.CommandLine
```

### 2. Build the fuzz project

```powershell
cd Q:\SDK\azure-cosmos-dotnet-v3
dotnet build Microsoft.Azure.Cosmos.FuzzTests -c Release -f net10.0
```

### 3. Instrument the SDK DLL

SharpFuzz needs to rewrite the target DLL's IL to insert coverage tracking.
Instrument the SDK assembly that your fuzz targets exercise:

```powershell
sharpfuzz Microsoft.Azure.Cosmos.FuzzTests\bin\Release\net10.0\Microsoft.Azure.Cosmos.Client.dll
```

> **Tip:** For best coverage visibility, also instrument any dependent DLLs
> your target exercises (e.g., `Microsoft.Azure.Cosmos.Direct.dll`).

### 4. Run coverage-guided fuzzing

Run libfuzzer-dotnet against one of the 7 targets. Example for the SQL parser:

```powershell
libfuzzer-dotnet `
    --target_path=Microsoft.Azure.Cosmos.FuzzTests\bin\Release\net10.0\Microsoft.Azure.Cosmos.FuzzTests.exe `
    --target_arg="--libfuzzer SqlQueryParserFuzz" `
    -max_total_time=300 `
    Microsoft.Azure.Cosmos.FuzzTests\seeds\sql-parser
```

> **Important:** `libfuzzer-dotnet` invokes the harness EXE, and our `Program.cs` then calls
> `Fuzzer.LibFuzzer.Run(...)`. The instrumented SDK DLL **must** be loaded through this code path —
> running it any other way (e.g., seed validation mode) will fail with `NullReferenceException`
> because the SharpFuzz runtime state is only initialized inside `Fuzzer.LibFuzzer.Run`.

libFuzzer will print output like:

```
#0       INITED   cov: 2 ft: 208 corp: 11/93b
#1024    NEW      cov: 2 ft: 217 corp: 12/101b   <-- found new code path
#16432   pulse    cov: 2 ft: 305 corp: 24/365b
#182870  DONE     cov: 2 ft: 315 corp: 25/389b   <-- 16,000 iterations/sec
```

If it finds a crash, the triggering input is saved as `crash-<hash>` in the current directory.

### 5. Reproduce a crash

```powershell
dotnet run --project Microsoft.Azure.Cosmos.FuzzTests -- `
    --target SqlQueryParserFuzz --input crash-abc123
```

This re-runs the harness against the single crashing input so you can attach
a debugger.

---

## Seed Validation (no `libfuzzer-dotnet` required)

For quick CI checks (or when you just want to verify seeds don't crash),
run the seed validation mode. **This requires a non-instrumented DLL** — do
NOT run `sharpfuzz` before this. If you already ran `sharpfuzz`, rebuild
first to restore the clean DLL: `dotnet build -c Release --no-incremental`.

```powershell
dotnet run --project Microsoft.Azure.Cosmos.FuzzTests -- `
    --target SqlQueryParserFuzz --seeds Microsoft.Azure.Cosmos.FuzzTests\seeds\sql-parser
```

This is what runs in the PR validation pipeline (`azure-pipelines-fuzzing.yml`).

---

## Available Fuzz Targets

| Target Name | SDK Parser Under Test |
|---|---|
| `SqlQueryParserFuzz` | `SqlQueryParser.Monadic.Parse` (ANTLR SQL grammar) |
| `JsonNavigatorFuzz` | `JsonNavigator.Create` (JSON text + binary) |
| `CosmosElementFuzz` | `CosmosElement.Monadic.CreateFromBuffer` (document DOM) |
| `FeedResponseFuzz` | Feed response envelope deserialization |
| `ErrorResponseFuzz` | Error response JSON deserialization |
| `PartitionKeyFuzz` | `new PartitionKey(string)` |
| `ResourceIdentifierFuzz` | `ResourceId.TryParse` |

---

## Project Structure

```
Microsoft.Azure.Cosmos.FuzzTests/
├── Microsoft.Azure.Cosmos.FuzzTests.csproj   # Console app, refs SDK + SharpFuzz
├── Program.cs                                 # Entry point (libFuzzer + seed validation modes)
├── IFuzzerTarget.cs                           # Shared interface and helpers
├── FuzzerValidationException.cs               # Used to signal logical bugs (vs. crashes)
├── Targets/                                   # The 7 fuzz targets
│   ├── SqlQueryParserFuzz.cs
│   ├── JsonNavigatorFuzz.cs
│   ├── CosmosElementFuzz.cs
│   ├── FeedResponseFuzz.cs
│   ├── ErrorResponseFuzz.cs
│   ├── PartitionKeyFuzz.cs
│   └── ResourceIdentifierFuzz.cs
├── seeds/                                     # Seed corpus per target
│   ├── sql-parser/
│   ├── json-parser/
│   ├── feed-response/
│   ├── error-response/
│   ├── partition-key/
│   └── resource-id/
└── dictionaries/                              # Token dictionaries for guided mutation
    ├── sql.dict
    └── json.dict
```

The fuzz pipeline (`../azure-pipelines-fuzzing.yml`) runs seed validation on every PR — it
builds this project and runs all 7 targets against their seed inputs. Any crash fails the PR.

---

## Adding a New Fuzz Target

1. Create `Targets/MyParserFuzz.cs` with the signature:

   ```csharp
   internal sealed class MyParserFuzz : IFuzzerTarget
   {
       public static void Fuzz(ReadOnlySpan<byte> input)
       {
           // 1. Convert input to the parser's input type
           // 2. Call the parser
           // 3. Catch ONLY expected exceptions
           // 4. Let unexpected exceptions propagate (those are bugs)
       }
   }
   ```

2. Add seed files to `seeds/my-parser/`
3. Add a step to `azure-pipelines-fuzzing.yml` to validate the new seeds on PR
4. Test locally with seed validation, then with `libfuzzer-dotnet`

---

## Best Practices

Per the OneFuzz and SharpFuzz docs:

- ✅ **Catch only expected exceptions** — a blanket `catch (Exception)` suppresses real bugs
- ✅ **Use `ReadOnlySpan<byte>`** — preferred over `byte[]` for OneFuzz compatibility
- ✅ **Method must be `public static void`** — required for libfuzzer-dotnet reflection
- ✅ **Instrument all dependent DLLs** — for full coverage visibility
- ✅ **Add seed corpus + dictionaries** — helps the fuzzer find code paths faster
- ✅ **Prefer libFuzzer mode over AFL** — works on Windows, Linux, and macOS

---

## Cross-Language Fuzzing (Future)

The same libFuzzer approach can be applied to the Java and Python Cosmos DB SDKs:

| Language | Tool | Harness Pattern |
|----------|------|-----------------|
| .NET | `libfuzzer-dotnet` + SharpFuzz | `public static void Fuzz(ReadOnlySpan<byte> input)` |
| Java | [Jazzer](https://github.com/CodeIntelligenceTesting/jazzer) | `public static void fuzzerTestOneInput(byte[] input)` |
| Python | [Atheris](https://github.com/google/atheris) | `def TestOneInput(data: bytes)` |

Seed files and dictionaries are shared across languages — they're just byte files.

---

## References

- **SharpFuzz GitHub**: https://github.com/Metalnem/sharpfuzz
- **libfuzzer-dotnet GitHub**: https://github.com/Metalnem/libfuzzer-dotnet
- **Fuzzing C# Code with SharpFuzz**: https://eng.ms/docs/cloud-ai-platform/azure-core/core-compute-and-host/specialized-compute-arunki/acc-tests-and-tools/tooling/fuzzing/fuzzing_csharp
- **OneFuzz — Fuzzing .NET Code**: https://eng.ms/docs/cloud-ai-platform/microsoft-specialized-clouds-msc/msc-security/security-fundamentals/the-onefuzz-service/onefuzz/fuzzeronboarding/notwindows/fuzzing-dotnet-code
- **OneFuzz Service Overview**: https://eng.ms/docs/cloud-ai-platform/microsoft-specialized-clouds-msc/msc-security/security-fundamentals/the-onefuzz-service/onefuzz
