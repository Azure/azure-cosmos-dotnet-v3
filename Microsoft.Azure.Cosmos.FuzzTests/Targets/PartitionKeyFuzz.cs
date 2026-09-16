//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FuzzTests.Targets
{
    using System;

    /// <summary>
    /// Fuzz target for <see cref="PartitionKey"/> construction.
    /// Tests partition key creation from arbitrary string values.
    /// </summary>
    /// <remarks>
    /// Scenarios covered:
    ///   - Very long strings
    ///   - Unicode characters (CJK, emoji, RTL)
    ///   - Null/empty strings
    ///   - JSON-like strings ("{}", "[]", "null", "true")
    ///   - Number-like strings ("NaN", "Infinity", "1e999")
    ///   - Strings with embedded null bytes
    ///
    /// Wrong partition key parsing leads to wrong routing, which can cause
    /// data to be written to the wrong partition — a silent data corruption bug.
    /// </remarks>
    internal sealed class PartitionKeyFuzz : IFuzzerTarget
    {
        public static void Fuzz(ReadOnlySpan<byte> input)
        {
            if (!IFuzzerTarget.PrepareInput(ref input))
            {
                return;
            }

            if (!IFuzzerTarget.TryGetString(input, out string value))
            {
                return;
            }

            try
            {
                PartitionKey pk = new PartitionKey(value);

                // If construction succeeds, validate that ToString doesn't crash
                string serialized = pk.ToString();

                // Validate that serialization doesn't crash
                _ = pk.Equals(pk);
            }
            catch (ArgumentNullException)
            {
                // Expected: null value
            }
            catch (ArgumentException)
            {
                // Expected: invalid value
            }
        }
    }
}
