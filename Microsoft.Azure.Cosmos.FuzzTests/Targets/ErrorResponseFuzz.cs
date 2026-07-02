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
    /// Fuzz target for error response parsing.
    /// Tests parsing of error JSON structures returned by the Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Scenarios covered:
    ///   - Missing "code" or "message" fields
    ///   - "code" as non-string type (number, object, array)
    ///   - Truncated JSON
    ///   - Very large error messages
    ///   - Nested error objects
    ///   - Non-JSON content (HTML, plain text, binary)
    ///   - Empty input
    ///
    /// Error parsing paths are often less hardened than success paths because
    /// they handle unexpected server responses. We parse error JSON as
    /// CosmosElement and validate the expected structure.
    /// </remarks>
    internal sealed class ErrorResponseFuzz : IFuzzerTarget
    {
        public static void Fuzz(ReadOnlySpan<byte> input)
        {
            if (!IFuzzerTarget.PrepareInput(ref input))
            {
                return;
            }

            // Parse the error response as a CosmosElement
            if (!IFuzzerTarget.TryGetString(input, out string json))
            {
                return;
            }

            TryCatch<CosmosElement> result = CosmosElement.Monadic.Parse(json);

            if (result.Succeeded && result.Result is CosmosObject errorObj)
            {
                try
                {
                    _ = errorObj.TryGetValue("code", out CosmosElement? code);
                    _ = errorObj.TryGetValue("message", out CosmosElement? message);
                    _ = errorObj.TryGetValue("activityId", out CosmosElement? activityId);

                    if (code is CosmosString)
                    {
                        _ = code.ToString();
                    }

                    if (message is CosmosString)
                    {
                        _ = message.ToString();
                    }

                    _ = errorObj.ToString();
                }
                catch (JsonParseException)
                {
                    // Lazy parsing can throw when materializing values
                }
            }
        }
    }
}
