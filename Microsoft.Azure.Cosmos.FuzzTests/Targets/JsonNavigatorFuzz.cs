//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FuzzTests.Targets
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Fuzz target for JSON parsing via <see cref="JsonNavigator"/> and <see cref="JsonReader"/>.
    /// Tests both text JSON and binary JSON parsing.
    /// </summary>
    /// <remarks>
    /// Scenarios covered:
    ///   - Text JSON: truncated strings, missing brackets, invalid escapes, huge numbers
    ///   - Binary JSON: corrupted type markers, huge length fields, invalid nesting
    ///   - Format auto-detection: bytes starting with 0x80 route to binary, others to text
    ///   - Deep nesting: [[[[... and {{{{... (stack overflow risk)
    ///   - Number edge cases: 1e999999, -0, NaN-like sequences
    ///   - String edge cases: UTF-16 surrogates, null bytes, overlong UTF-8
    ///
    /// We fuzz via CosmosElement.CreateFromBuffer which internally uses JsonNavigator,
    /// ensuring the full parsing pipeline is exercised.
    /// </remarks>
    internal sealed class JsonNavigatorFuzz : IFuzzerTarget
    {
        public static void Fuzz(ReadOnlySpan<byte> input)
        {
            if (!IFuzzerTarget.PrepareInput(ref input))
            {
                return;
            }

            // Fuzz via CosmosElement which internally calls JsonNavigator.Create
            // and walks the tree. This exercises the full JSON parsing pipeline.
            TryCatch<CosmosElement> result = CosmosElement.Monadic.CreateFromBuffer(
                new ReadOnlyMemory<byte>(input.ToArray()));

            if (result.Succeeded)
            {
                // Force full materialization by serializing to string
                _ = result.Result.ToString();
            }

            // Also test the text JSON reader path if the input looks like text
            FuzzJsonReader(input);
        }

        private static void FuzzJsonReader(ReadOnlySpan<byte> input)
        {
            try
            {
                IJsonReader reader = JsonReader.Create(new ReadOnlyMemory<byte>(input.ToArray()));

                // Read all tokens to exercise the reader
                int tokenCount = 0;
                while (reader.Read() && tokenCount < 100_000)
                {
                    _ = reader.CurrentTokenType;
                    tokenCount++;
                }
            }
            catch (Exception ex) when (ex is JsonParseException || ex is ArgumentException)
            {
                // Expected: malformed JSON or invalid data
            }
        }
    }
}
