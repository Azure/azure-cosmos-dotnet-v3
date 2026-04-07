//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Guards against introducing bare Expression.Compile() calls in the LINQ provider.
    /// All Compile() calls must use Compile(preferInterpretation: true) to avoid native
    /// memory leaks from DynamicMethod IL emission.
    /// See: https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5487
    /// </summary>
    [TestClass]
    public class LinqCompileGuardTests
    {
        // Matches .Compile() with no arguments — the problematic pattern
        private static readonly Regex BareCompilePattern = new Regex(
            @"\.Compile\(\s*\)",
            RegexOptions.Compiled);

        // Matches .Compile(preferInterpretation: true) — the correct pattern
        private static readonly Regex InterpretedCompilePattern = new Regex(
            @"\.Compile\(\s*preferInterpretation\s*:\s*true\s*\)",
            RegexOptions.Compiled);

        [TestMethod]
        public void LinqSourceFiles_ShouldNotUseBareCompile()
        {
            string linqDirectory = Path.GetFullPath(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..", "..", "..", "..", "..",
                    "src", "Linq"));

            Assert.IsTrue(
                Directory.Exists(linqDirectory),
                $"LINQ source directory not found at: {linqDirectory}");

            string[] sourceFiles = Directory.GetFiles(linqDirectory, "*.cs", SearchOption.AllDirectories);
            Assert.IsTrue(sourceFiles.Length > 0, "No source files found in LINQ directory.");

            List<string> violations = new List<string>();

            foreach (string file in sourceFiles)
            {
                string[] lines = File.ReadAllLines(file);
                string fileName = Path.GetFileName(file);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];

                    if (BareCompilePattern.IsMatch(line) && !InterpretedCompilePattern.IsMatch(line))
                    {
                        violations.Add($"  {fileName}:{i + 1} => {line.Trim()}");
                    }
                }
            }

            Assert.AreEqual(
                0,
                violations.Count,
                $"Found bare .Compile() calls without preferInterpretation: true. " +
                $"Use .Compile(preferInterpretation: true) to avoid native memory leaks " +
                $"(see issue #5487):\n{string.Join("\n", violations)}");
        }
    }
}
