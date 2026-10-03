//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FuzzTests.Targets
{
    using System;
    using Microsoft.Azure.Cosmos.Pagination;

    /// <summary>
    /// Fuzz target for <see cref="ResourceIdentifier"/> parsing.
    /// Tests the base64-encoded resource ID parser that handles manual byte manipulation.
    /// </summary>
    /// <remarks>
    /// Scenarios covered:
    ///   - Invalid base64 strings (wrong padding, invalid chars)
    ///   - Valid base64 but wrong length (not 20 bytes)
    ///   - Truncated IDs
    ///   - Empty string
    ///   - Very long strings
    ///   - Round-trip: parse → toString → re-parse consistency
    ///
    /// Resource IDs use a custom 20-byte binary format with specific bit-level
    /// semantics for database/collection/document hierarchy. Manual byte
    /// manipulation in the parser makes this a good fuzz target.
    /// </remarks>
    internal sealed class ResourceIdentifierFuzz : IFuzzerTarget
    {
        public static void Fuzz(ReadOnlySpan<byte> input)
        {
            if (!IFuzzerTarget.PrepareInput(ref input))
            {
                return;
            }

            if (!IFuzzerTarget.TryGetString(input, out string id))
            {
                return;
            }

            try
            {
                bool success = ResourceIdentifier.TryParse(id, out ResourceIdentifier rid);

                if (success)
                {
                    // Round-trip validation
                    string roundTripped = rid.ToString();
                    bool roundTripSuccess = ResourceIdentifier.TryParse(roundTripped, out ResourceIdentifier rid2);

                    if (!roundTripSuccess)
                    {
                        IFuzzerTarget.RaiseErrorForInput(
                            $"ResourceIdentifier round-trip failure. " +
                            $"Original '{id}' parsed OK, but re-serialized form '{roundTripped}' failed.",
                            input);
                    }
                }
            }
            catch (ArgumentException)
            {
                // Expected: invalid ID format
            }
            catch (FormatException)
            {
                // Expected: invalid base64
            }
        }
    }
}
