//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FuzzTests
{
    using System;
    using System.IO;
    using System.Reflection;
    using SharpFuzz;

    /// <summary>
    /// Entry point for fuzz testing the Azure Cosmos DB .NET SDK.
    ///
    /// Two modes of operation (both selected via command-line args, no env vars):
    ///
    /// 1. LIBFUZZER MODE (coverage-guided fuzzing):
    ///    Runs with libfuzzer-dotnet for continuous, coverage-guided mutation testing.
    ///    Requires the SDK DLL to have been instrumented with `sharpfuzz` first.
    ///
    ///    Usage (driven by libfuzzer-dotnet):
    ///      libfuzzer-dotnet `
    ///          --target_path=bin/Release/net10.0/Microsoft.Azure.Cosmos.FuzzTests.exe `
    ///          --target_arg="--libfuzzer SqlQueryParserFuzz" `
    ///          -max_total_time=300 `
    ///          seeds/sql-parser
    ///
    /// 2. SEED VALIDATION / CRASH REPRO MODE:
    ///    Runs each input through the harness once on a clean (non-instrumented) DLL
    ///    so .NET stack traces are pristine.
    ///
    ///    Usage:
    ///      dotnet run -- --target SqlQueryParserFuzz --seeds seeds/sql-parser
    ///      dotnet run -- --target SqlQueryParserFuzz --input crash-file.bin
    /// </summary>
    public static class Program
    {
        private delegate void FuzzTargetDelegate(ReadOnlySpan<byte> input);

        public static void Main(string[] args)
        {
            // ── OneFuzz compatibility (Phase 2) ──────────────────────────────────
            // OneFuzz's LibFuzzerDotnetLoader convention passes the target class via
            // env vars instead of CLI args. Local fuzzing (Phase 1) uses --libfuzzer
            // <Target> and never sets these; OneFuzz cloud submission will.
            // Kept intentionally so Phase 2 needs zero code changes.
            string? envTargetClass = Environment.GetEnvironmentVariable("LIBFUZZER_DOTNET_TARGET_CLASS");
            if (!string.IsNullOrEmpty(envTargetClass))
            {
                string envSimpleName = envTargetClass.Contains('.')
                    ? envTargetClass[(envTargetClass.LastIndexOf('.') + 1)..]
                    : envTargetClass;
                FuzzTargetDelegate envTarget = ResolveTarget(envSimpleName);
                Fuzzer.LibFuzzer.Run(span => envTarget(span));
                return;
            }

            // ── CLI modes (Phase 1, local dev) ───────────────────────────────────
            //   --libfuzzer <Target>                       → coverage-guided fuzzing
            //   --target    <Target> --seeds <directory>   → seed validation
            //   --target    <Target> --input  <file>       → crash reproduction
            if (args.Length < 2)
            {
                PrintUsage();
                Environment.Exit(1);
            }

            string mode = args[0];
            string targetName = args[1];

            FuzzTargetDelegate target = ResolveTarget(targetName);

            switch (mode)
            {
                case "--libfuzzer":
                    // Hand control to the SharpFuzz/libFuzzer loop. Requires the SDK DLL
                    // to have been instrumented with `sharpfuzz` first.
                    Fuzzer.LibFuzzer.Run(span => target(span));
                    break;

                case "--target":
                    // Seed validation / crash reproduction. Uses a clean (non-instrumented)
                    // DLL so .NET stack traces are pristine.
                    if (args.Length < 4)
                    {
                        PrintUsage();
                        Environment.Exit(1);
                    }

                    string inputMode = args[2];
                    string path = args[3];

                    RunSeedValidation(target, targetName, inputMode, path);
                    break;

                default:
                    PrintUsage();
                    Environment.Exit(1);
                    break;
            }
        }

        private static void RunSeedValidation(
            FuzzTargetDelegate target, string targetName, string inputMode, string path)
        {
            byte[][] inputs = inputMode switch
            {
                "--seeds" => LoadDirectory(path),
                "--input" => new[] { File.ReadAllBytes(path) },
                _ => throw new ArgumentException($"Unknown input mode: {inputMode}")
            };

            Console.WriteLine($"Running {targetName} against {inputs.Length} input(s)...");

            int passed = 0;
            int crashed = 0;

            foreach (byte[] input in inputs)
            {
                try
                {
                    target(input);
                    passed++;
                }
                catch (Exception ex)
                {
                    crashed++;
                    Console.Error.WriteLine($"CRASH: {ex.GetType().Name}: {ex.Message}");
                    Console.Error.WriteLine(ex.StackTrace);
                }
            }

            Console.WriteLine($"Done: {passed} passed, {crashed} crashed.");

            if (crashed > 0)
            {
                Environment.Exit(1);
            }
        }

        private static byte[][] LoadDirectory(string dir)
        {
            string[] files = Directory.GetFiles(dir);
            byte[][] inputs = new byte[files.Length][];
            for (int i = 0; i < files.Length; i++)
            {
                inputs[i] = File.ReadAllBytes(files[i]);
            }

            return inputs;
        }

        private static FuzzTargetDelegate ResolveTarget(string targetName)
        {
            string fullTypeName = $"Microsoft.Azure.Cosmos.FuzzTests.Targets.{targetName}";
            Type? type = Assembly.GetExecutingAssembly().GetType(fullTypeName);

            if (type == null)
            {
                Console.Error.WriteLine($"Error: Target '{targetName}' not found.");
                Console.Error.WriteLine("Available targets:");
                foreach (Type t in Assembly.GetExecutingAssembly().GetTypes())
                {
                    if (t.Namespace == "Microsoft.Azure.Cosmos.FuzzTests.Targets"
                        && t.GetMethod("Fuzz", BindingFlags.Public | BindingFlags.Static) != null)
                    {
                        Console.Error.WriteLine($"  {t.Name}");
                    }
                }

                Environment.Exit(2);
                return null!;
            }

            MethodInfo method = type.GetMethod("Fuzz", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Target '{targetName}' has no static Fuzz method");

            return (FuzzTargetDelegate)Delegate.CreateDelegate(typeof(FuzzTargetDelegate), null, method);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Azure Cosmos DB .NET SDK Fuzz Tests");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Seed validation:  dotnet run -- --target <Target> --seeds <directory>");
            Console.WriteLine("  Crash repro:      dotnet run -- --target <Target> --input <file>");
            Console.WriteLine("  libFuzzer mode:   libfuzzer-dotnet --target_path=<dll> --target_class=<class> --target_method=Fuzz <corpus_dir>");
            Console.WriteLine();
            Console.WriteLine("Targets: SqlQueryParserFuzz, JsonNavigatorFuzz, CosmosElementFuzz,");
            Console.WriteLine("         FeedResponseFuzz, ErrorResponseFuzz, PartitionKeyFuzz,");
            Console.WriteLine("         ResourceIdentifierFuzz");
        }
    }
}
