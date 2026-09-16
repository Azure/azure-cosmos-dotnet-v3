//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FuzzTests.Targets
{
    using System;
    using System.IO;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Fuzz target for feed response parsing.
    /// Tests parsing of REST API response envelopes via CosmosElement.
    /// </summary>
    /// <remarks>
    /// Scenarios covered:
    ///   - Missing "_rid", "Documents", "_count" fields
    ///   - "Documents" as non-array type
    ///   - Truncated response stream
    ///   - Empty stream
    ///   - Very large document arrays
    ///   - Binary JSON encoded responses
    ///   - Malformed JSON with valid-looking structure
    ///
    /// Feed responses are the most common server response format.
    /// We parse them as CosmosElement and validate the expected structure.
    /// </remarks>
    internal sealed class FeedResponseFuzz : IFuzzerTarget
    {
        public static void Fuzz(ReadOnlySpan<byte> input)
        {
            if (!IFuzzerTarget.PrepareInput(ref input))
            {
                return;
            }

            // Parse the input as a CosmosElement (same path as feed responses)
            TryCatch<CosmosElement> result = CosmosElement.Monadic.CreateFromBuffer(
                new ReadOnlyMemory<byte>(input.ToArray()));

            if (result.Succeeded)
            {
                try
                {
                    // If it parsed as an object, try to extract feed response fields
                    if (result.Result is CosmosObject obj)
                    {
                        _ = obj.TryGetValue("_rid", out CosmosElement? ridElement);

                        if (obj.TryGetValue("Documents", out CosmosElement? docsElement)
                            && docsElement is CosmosArray docsArray)
                        {
                            _ = docsArray.Count;
                            foreach (CosmosElement doc in docsArray)
                            {
                                _ = doc.ToString();
                            }
                        }

                        _ = obj.TryGetValue("_count", out CosmosElement? countElement);
                    }

                    _ = result.Result.ToString();
                }
                catch (JsonParseException)
                {
                    // Lazy parsing can throw when materializing values
                }
            }
        }
    }
}
