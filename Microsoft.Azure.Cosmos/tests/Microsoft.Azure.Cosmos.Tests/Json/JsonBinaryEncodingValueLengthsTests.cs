//-----------------------------------------------------------------------
// <copyright file="JsonBinaryEncodingValueLengthsTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Regression tests for the depth guard in
    /// <see cref="JsonBinaryEncoding"/>'s value-length decoder. Without the
    /// guard a crafted payload consisting of nested Arr1 (0xE1) or Obj1 (0xE9)
    /// type markers would recursively call GetValueLength until the CLR stack
    /// is exhausted and the process aborts with StackOverflowException.
    /// </summary>
    [TestClass]
    public class JsonBinaryEncodingValueLengthsTests
    {
        private const byte BinaryFormatMarker = 0x80;
        private const byte Arr0TypeMarker = 0xE0;
        private const byte Arr1TypeMarker = 0xE1;
        private const byte Obj0TypeMarker = 0xE8;
        private const byte Obj1TypeMarker = 0xE9;

        [TestMethod]
        [Owner("tvaron")]
        public void DeeplyNestedArr1PayloadThrowsInsteadOfStackOverflow()
        {
            // 80,000 nested Arr1 markers terminated by an empty Arr0 -- the
            // payload from the original vulnerability report. Should be
            // rejected by the depth guard rather than crashing the process.
            byte[] payload = BuildArr1ChainPayload(nestingDepth: 80_000);

            Assert.ThrowsException<JsonMaxNestingExceededException>(
                () => JsonBinaryEncoding.GetValueLength(payload.AsSpan(start: 1)));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void DeeplyNestedObj1PayloadThrowsInsteadOfStackOverflow()
        {
            byte[] payload = BuildObj1ChainPayload(nestingDepth: 80_000);

            Assert.ThrowsException<JsonMaxNestingExceededException>(
                () => JsonBinaryEncoding.GetValueLength(payload.AsSpan(start: 1)));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void DepthAtCapThrows()
        {
            // Exactly JsonMaxNestingDepth nested Arr1 levels must trip the
            // guard. The outermost Arr1 is processed at depth 0; each nested
            // call increments depth, and the guard fires when GetValueLength
            // is entered with depth >= JsonMaxNestingDepth (i.e., when
            // attempting to process the content inside the 256th Arr1).
            byte[] payload = BuildArr1ChainPayload(nestingDepth: JsonObjectState.JsonMaxNestingDepth);

            Assert.ThrowsException<JsonMaxNestingExceededException>(
                () => JsonBinaryEncoding.GetValueLength(payload.AsSpan(start: 1)));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void DepthJustBelowCapStillDecodes()
        {
            // JsonMaxNestingDepth - 1 nested Arr1 levels plus the terminating
            // Arr0 must still decode successfully so legitimate (deep but
            // bounded) documents are not broken by the guard.
            int safeDepth = JsonObjectState.JsonMaxNestingDepth - 1;
            byte[] payload = BuildArr1ChainPayload(nestingDepth: safeDepth);

            int length = JsonBinaryEncoding.GetValueLength(payload.AsSpan(start: 1));

            // Each Arr1 contributes 1 byte, plus the terminating Arr0 byte.
            Assert.AreEqual(safeDepth + 1, length);
        }

        [TestMethod]
        [Owner("tvaron")]
        public void DeeplyNestedArr1PayloadFailsBinaryNavigatorConstruction()
        {
            // Exercise the actual entry point hit when a hostile endpoint
            // returns a 0x80-prefixed binary payload: constructing the
            // JsonBinaryNavigator must not crash the process.
            byte[] payload = BuildArr1ChainPayload(nestingDepth: 80_000);

            Assert.ThrowsException<JsonMaxNestingExceededException>(
                () => JsonNavigator.Create(payload));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void TryGetValueLengthReturnsFalseForDeeplyNestedPayload()
        {
            byte[] payload = BuildArr1ChainPayload(nestingDepth: 80_000);

            bool succeeded = JsonBinaryEncoding.TryGetValueLength(
                payload.AsSpan(start: 1),
                out int length);

            Assert.IsFalse(succeeded);
            Assert.AreEqual(0, length);
        }

        [TestMethod]
        [Owner("tvaron")]
        public void TryGetValueLengthReturnsFalseForEmptyBuffer()
        {
            // Empty span would otherwise raise IndexOutOfRangeException out of
            // the Lookup[buffer[0]] dereference. The Try-pattern must swallow
            // it.
            bool succeeded = JsonBinaryEncoding.TryGetValueLength(
                ReadOnlySpan<byte>.Empty,
                out int length);

            Assert.IsFalse(succeeded);
            Assert.AreEqual(0, length);
        }

        [TestMethod]
        [Owner("tvaron")]
        public void TryGetValueLengthReturnsFalseForTruncatedObj1Payload()
        {
            // Obj1 marker with a name length that runs past the end of the
            // buffer would raise ArgumentOutOfRangeException from the second
            // GetValueLength's buffer.Slice call. The Try-pattern must swallow
            // it.
            byte[] payload = new byte[] { Obj1TypeMarker, 0xC0 /* StrL1 */, 0xFF /* claimed length = 255 */ };

            bool succeeded = JsonBinaryEncoding.TryGetValueLength(payload, out int length);

            Assert.IsFalse(succeeded);
            Assert.AreEqual(0, length);
        }

        [TestMethod]
        [Owner("tvaron")]
        public void DeeplyNestedArr1PayloadFailsBinaryReaderConstruction()
        {
            // The JsonBinaryReader constructor is the other public entry
            // point hit by malicious payloads (e.g. via CosmosElement.Parse).
            // It must also fail cleanly instead of crashing the process.
            byte[] payload = BuildArr1ChainPayload(nestingDepth: 80_000);

            Assert.ThrowsException<JsonMaxNestingExceededException>(
                () => JsonReader.Create(payload));
        }

        private static byte[] BuildArr1ChainPayload(int nestingDepth)
        {
            // Layout: 0x80 (binary format) | nestingDepth * 0xE1 | 0xE0
            byte[] payload = new byte[1 + nestingDepth + 1];
            payload[0] = BinaryFormatMarker;
            for (int i = 0; i < nestingDepth; i++)
            {
                payload[1 + i] = Arr1TypeMarker;
            }

            payload[payload.Length - 1] = Arr0TypeMarker;
            return payload;
        }

        private static byte[] BuildObj1ChainPayload(int nestingDepth)
        {
            // Layout: 0x80 (binary format) | nestingDepth * 0xE9 | 0xE8
            // Note: Obj1 expects a (name, value) pair, so this chain is only
            // structurally valid as far as the depth guard reaches -- below
            // the guarded depth the innermost Obj1 has no value byte. That is
            // intentional for the depth-guard test (the guard fires before the
            // malformed bottom is reached); do NOT reuse this helper for tests
            // that rely on a shallow Obj1 chain being well-formed.
            byte[] payload = new byte[1 + nestingDepth + 1];
            payload[0] = BinaryFormatMarker;
            for (int i = 0; i < nestingDepth; i++)
            {
                payload[1 + i] = Obj1TypeMarker;
            }

            payload[payload.Length - 1] = Obj0TypeMarker;
            return payload;
        }
    }
}
