//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FuzzTests
{
    using System;
    using System.Text;

    /// <summary>
    /// Contract for fuzz targets. Each target implements a static <see cref="Fuzz"/> method
    /// that OneFuzz invokes directly via <c>libfuzzerDotNet</c>.
    /// </summary>
    /// <remarks>
    /// Pattern adopted from the Garnet fuzzing infrastructure in the CosmosDB organization.
    /// OneFuzz config references the class name and "Fuzz" method by convention.
    /// </remarks>
    internal interface IFuzzerTarget
    {
        /// <summary>
        /// Entry point invoked by the fuzzer with mutated input bytes.
        /// Implementations must:
        ///   1. Convert bytes to the target input type (string, Stream, etc.)
        ///   2. Call the parser/deserializer under test
        ///   3. Catch ONLY exceptions the parser is designed to throw
        ///   4. Let all unexpected exceptions propagate (they are real bugs)
        ///   5. Optionally validate invariants when parsing succeeds
        /// </summary>
        static abstract void Fuzz(ReadOnlySpan<byte> input);

        /// <summary>
        /// Shared input preparation. Skips empty inputs that are uninteresting.
        /// </summary>
        /// <returns>False if input should be skipped.</returns>
        static bool PrepareInput(ref ReadOnlySpan<byte> input)
        {
            return input.Length > 0;
        }

        /// <summary>
        /// Raises an error that includes the triggering input for debugging.
        /// Use this to signal logical bugs (e.g., round-trip failures) rather than crashes.
        /// </summary>
        static void RaiseErrorForInput(string message, ReadOnlySpan<byte> input)
        {
            string inputHex = input.Length <= 200
                ? BitConverter.ToString(input.ToArray())
                : BitConverter.ToString(input[..200].ToArray()) + "... (truncated)";

            throw new FuzzerValidationException($"{message}\nInput ({input.Length} bytes): {inputHex}");
        }

        /// <summary>
        /// Wraps an unexpected exception with input context for local debugging.
        /// </summary>
        static void RaiseErrorForInput(Exception inner, ReadOnlySpan<byte> input)
        {
            string inputHex = input.Length <= 200
                ? BitConverter.ToString(input.ToArray())
                : BitConverter.ToString(input[..200].ToArray()) + "... (truncated)";

            throw new FuzzerValidationException(
                $"Unexpected {inner.GetType().Name}: {inner.Message}\nInput ({input.Length} bytes): {inputHex}",
                inner);
        }

        /// <summary>
        /// Attempts UTF-8 conversion. Returns false if the bytes are not valid UTF-8.
        /// </summary>
        static bool TryGetString(ReadOnlySpan<byte> input, out string result)
        {
            try
            {
                result = Encoding.UTF8.GetString(input);
                return true;
            }
            catch (DecoderFallbackException)
            {
                result = string.Empty;
                return false;
            }
            catch (ArgumentException)
            {
                result = string.Empty;
                return false;
            }
        }
    }
}
