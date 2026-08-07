//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FuzzTests.Targets
{
    using System;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Fuzz target for <see cref="CosmosElement"/> parsing.
    /// Tests the highest-level document element parser used throughout the SDK.
    /// </summary>
    /// <remarks>
    /// Scenarios covered:
    ///   - String path: arbitrary JSON text via CosmosElement.Monadic.Parse(string)
    ///   - Buffer path: arbitrary bytes via CosmosElement.Monadic.CreateFromBuffer(ReadOnlyMemory)
    ///   - Round-trip: parse → ToString → re-parse must be consistent
    ///   - Type coercion edge cases (numbers, booleans, nulls)
    ///   - Mixed text/binary format confusion
    /// </remarks>
    internal sealed class CosmosElementFuzz : IFuzzerTarget
    {
        public static void Fuzz(ReadOnlySpan<byte> input)
        {
            if (!IFuzzerTarget.PrepareInput(ref input))
            {
                return;
            }

            FuzzBufferPath(input);
            FuzzStringPath(input);
        }

        /// <summary>
        /// Fuzzes the binary/raw buffer parsing path.
        /// This handles both text and binary JSON via auto-detection.
        /// </summary>
        private static void FuzzBufferPath(ReadOnlySpan<byte> input)
        {
            TryCatch<CosmosElement> result = CosmosElement.Monadic.CreateFromBuffer(
                new ReadOnlyMemory<byte>(input.ToArray()));

            // Success or failure via TryCatch — any THROWN exception is a bug.
            // No catch needed; TryCatch encapsulates errors.
        }

        /// <summary>
        /// Fuzzes the string-based JSON parsing path.
        /// Includes round-trip validation on success.
        /// </summary>
        private static void FuzzStringPath(ReadOnlySpan<byte> input)
        {
            if (!IFuzzerTarget.TryGetString(input, out string json))
            {
                return;
            }

            TryCatch<CosmosElement> result = CosmosElement.Monadic.Parse(json);

            if (result.Succeeded)
            {
                // Round-trip validation: the element should serialize back
                // to a string that re-parses successfully
                string serialized = result.Result.ToString();
                TryCatch<CosmosElement> roundTrip = CosmosElement.Monadic.Parse(serialized);

                if (roundTrip.Failed)
                {
                    IFuzzerTarget.RaiseErrorForInput(
                        $"CosmosElement round-trip failure.\n" +
                        $"Original parsed OK, but serialized form failed.\n" +
                        $"Serialized: {serialized[..Math.Min(serialized.Length, 200)]}",
                        input);
                }
            }
        }
    }
}
