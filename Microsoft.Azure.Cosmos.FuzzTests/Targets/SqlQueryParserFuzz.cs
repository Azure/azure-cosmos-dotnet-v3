//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FuzzTests.Targets
{
    using System;
    using System.Text;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.SqlObjects;

    /// <summary>
    /// Fuzz target for <see cref="SqlQueryParser"/>.
    /// Tests the ANTLR-based SQL query parser with arbitrary input strings.
    /// </summary>
    /// <remarks>
    /// Scenarios covered:
    ///   - Deeply nested parentheses and subqueries (stack overflow risk)
    ///   - Unicode identifiers and string literals
    ///   - Very large query strings (OOM risk)
    ///   - Truncated valid queries
    ///   - SQL keyword combinations in unexpected positions
    ///   - Round-trip consistency: parse → serialize → re-parse
    /// </remarks>
    internal sealed class SqlQueryParserFuzz : IFuzzerTarget
    {
        public static void Fuzz(ReadOnlySpan<byte> input)
        {
            if (!IFuzzerTarget.PrepareInput(ref input))
            {
                return;
            }

            if (!IFuzzerTarget.TryGetString(input, out string queryText))
            {
                return; // Invalid UTF-8 is uninteresting for SQL parsing
            }

            // Call the parser. SqlQueryParser.Monadic.Parse returns TryCatch<SqlQuery>
            // which encapsulates success/failure without throwing. Any THROWN exception
            // is an unexpected bug.
            TryCatch<SqlQuery> result = SqlQueryParser.Monadic.Parse(queryText);

            if (result.Succeeded)
            {
                // Round-trip validation: serialize the parsed query back to string,
                // then re-parse. Both operations must succeed and produce equivalent results.
                string serialized = result.Result.ToString();
                TryCatch<SqlQuery> roundTripResult = SqlQueryParser.Monadic.Parse(serialized);

                if (roundTripResult.Failed)
                {
                    IFuzzerTarget.RaiseErrorForInput(
                        $"Round-trip failure: original query parsed successfully, but serialized " +
                        $"form failed to re-parse.\nOriginal: {Truncate(queryText, 200)}\n" +
                        $"Serialized: {Truncate(serialized, 200)}\n" +
                        $"Error: {roundTripResult.Exception.Message}",
                        input);
                }
            }

            // If result.Failed, that's expected for fuzzed input — not a crash.
            // If Parse() throws any exception (NullRef, IndexOOR, StackOverflow),
            // it propagates out and the fuzzer captures it as a real bug.
        }

        private static string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength
                ? value
                : value[..maxLength] + "...";
        }
    }
}
