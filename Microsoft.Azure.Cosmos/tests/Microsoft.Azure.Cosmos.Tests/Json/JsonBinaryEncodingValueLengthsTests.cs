//-----------------------------------------------------------------------
// <copyright file="JsonBinaryEncodingValueLengthsTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
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

        [TestMethod]
        [Owner("tvaron")]
        public void DepthAtCapObj1Throws()
        {
            // Mirror of DepthAtCapThrows for the Obj1 (0xE9) code path. The
            // guard fires before the malformed bottom of the chain is reached.
            byte[] payload = BuildObj1ChainPayload(nestingDepth: JsonObjectState.JsonMaxNestingDepth);

            Assert.ThrowsException<JsonMaxNestingExceededException>(
                () => JsonBinaryEncoding.GetValueLength(payload.AsSpan(start: 1)));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void DeeplyNestedCosmosArrayHashThrowsCatchableException()
        {
            // Even with the decode-time guard in place, length-prefixed array
            // markers (ArrL1/L2/L4) skip the depth guard because their length
            // is read from a length prefix rather than discovered recursively.
            // A hostile endpoint could therefore still return a deeply-nested
            // payload that materializes cleanly and only blows the stack later
            // -- when the customer calls DISTINCT, puts the element in a
            // HashSet, or compares two such results. EnsureSufficientExecutionStack
            // on the recursive walkers must turn that uncatchable
            // StackOverflowException into a catchable InsufficientExecutionStackException.
            //
            // Run the recursion on an explicitly small-stack thread so the
            // assertion does not depend on the host thread's default stack size
            // (Linux main threads have 8 MB stacks; .NET ThreadPool workers
            // have 1.5 MB; macOS non-main pthreads have 512 KB). A 256 KB
            // dedicated stack guarantees the guard fires regardless of host.
            CosmosArray deeplyNested = CosmosArray.Create();
            for (int i = 0; i < 10_000; i++)
            {
                deeplyNested = CosmosArray.Create(new[] { (CosmosElement)deeplyNested });
            }

            Exception caught = null;
            System.Threading.Thread thread = new System.Threading.Thread(
                () =>
                {
                    try
                    {
                        global::Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct.DistinctHash.GetHash(deeplyNested);
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                },
                maxStackSize: 256 * 1024);
            thread.Start();
            thread.Join();

            Assert.IsInstanceOfType(caught, typeof(InsufficientExecutionStackException));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void DeeplyNestedCosmosArrayToQueryLiteralThrowsCatchableException()
        {
            // Companion to DeeplyNestedCosmosArrayHashThrowsCatchableException:
            // CosmosElementToQueryLiteral.Visit(CosmosArray|CosmosObject) is the
            // visitor that serializes a CosmosElement back to a query-string
            // literal (used by the cross-partition OrderBy pipeline when it
            // re-emits sort-key values into a continuation token). It walks the
            // element graph recursively, so without an EnsureSufficientExecutionStack
            // guard a hostile order-by result could turn into an unrecoverable
            // StackOverflowException during continuation-token construction.
            CosmosArray deeplyNested = CosmosArray.Create();
            for (int i = 0; i < 10_000; i++)
            {
                deeplyNested = CosmosArray.Create(new[] { (CosmosElement)deeplyNested });
            }

            Exception caught = null;
            System.Threading.Thread thread = new System.Threading.Thread(
                () =>
                {
                    try
                    {
                        System.Text.StringBuilder builder = new System.Text.StringBuilder();
                        global::Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy.CosmosElementToQueryLiteral visitor =
                            new global::Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy.CosmosElementToQueryLiteral(builder);
                        deeplyNested.Accept(visitor);
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                },
                maxStackSize: 256 * 1024);
            thread.Start();
            thread.Join();

            Assert.IsInstanceOfType(caught, typeof(InsufficientExecutionStackException));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void DeeplyNestedCosmosObjectToQueryLiteralThrowsCatchableException()
        {
            // Object-shaped companion to DeeplyNestedCosmosArrayToQueryLiteralThrowsCatchableException:
            // exercises the Visit(CosmosObject) branch of the same visitor.
            // Built outside-in so the innermost object value is the seed empty
            // object and each level wraps the previous in a single-property
            // CosmosObject.
            CosmosElement deeplyNested = CosmosObject.Create(new Dictionary<string, CosmosElement>());
            for (int i = 0; i < 10_000; i++)
            {
                deeplyNested = CosmosObject.Create(new Dictionary<string, CosmosElement> { ["n"] = deeplyNested });
            }

            Exception caught = null;
            System.Threading.Thread thread = new System.Threading.Thread(
                () =>
                {
                    try
                    {
                        System.Text.StringBuilder builder = new System.Text.StringBuilder();
                        global::Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy.CosmosElementToQueryLiteral visitor =
                            new global::Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy.CosmosElementToQueryLiteral(builder);
                        deeplyNested.Accept(visitor);
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                },
                maxStackSize: 256 * 1024);
            thread.Start();
            thread.Join();

            Assert.IsInstanceOfType(caught, typeof(InsufficientExecutionStackException));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void DeeplyNestedDeserializationVisitorThrowsCatchableException()
        {
            // End-to-end attack-chain regression test for JsonSerializer.DeserializationVisitor.
            // Build a binary payload with thousands of nested ArrL4 (length-
            // prefixed array) markers. The decoder's depth guard does not
            // protect against length-prefixed arrays (their child count is
            // declared, not discovered recursively), so without the walker
            // guard the recursive deserialization would tear down the host
            // with StackOverflowException. With the guard in place this
            // surfaces as a catchable InsufficientExecutionStackException
            // wrapped in the visitor's TryCatch result.
            const int Depth = 10_000;
            byte[] payload = BuildArrL4ChainPayload(Depth);

            Exception caught = null;
            System.Threading.Thread thread = new System.Threading.Thread(
                () =>
                {
                    try
                    {
                        Microsoft.Azure.Cosmos.Json.JsonSerializer.Monadic.Deserialize<System.Collections.Generic.IReadOnlyList<object>>(payload).ThrowIfFailed();
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                },
                maxStackSize: 256 * 1024);
            thread.Start();
            thread.Join();

            Assert.IsInstanceOfType(caught, typeof(InsufficientExecutionStackException));
        }

        private static byte[] BuildArrL4ChainPayload(int depth)
        {
            // Layout:
            //   [0]            0x80                       binary format
            //   then `depth` frames of:
            //     [pos]        0xE4                       ArrL4
            //     [pos+1..+4]  length (uint32 LE)         bytes-after-this-prefix to end
            //   final byte:    0xE0                       Arr0 (innermost empty array)
            int totalLength = 1 + (depth * 5) + 1;
            byte[] payload = new byte[totalLength];
            payload[0] = BinaryFormatMarker;
            int pos = 1;
            for (int i = 0; i < depth; i++)
            {
                payload[pos++] = 0xE4;
                int len = ((depth - 1 - i) * 5) + 1;
                payload[pos++] = (byte)(len & 0xFF);
                payload[pos++] = (byte)((len >> 8) & 0xFF);
                payload[pos++] = (byte)((len >> 16) & 0xFF);
                payload[pos++] = (byte)((len >> 24) & 0xFF);
            }

            payload[pos] = Arr0TypeMarker;
            return payload;
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
