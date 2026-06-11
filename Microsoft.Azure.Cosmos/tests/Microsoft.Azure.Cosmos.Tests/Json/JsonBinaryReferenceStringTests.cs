//-----------------------------------------------------------------------
// <copyright file="JsonBinaryReferenceStringTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Regression tests for the reference-string (StrR1/2/3/4) dereference
    /// guard in <c>JsonWriter.JsonBinaryWriter.ForceRewriteRawJsonValue</c>.
    ///
    /// The binary writer's <c>FixReferenceStringOffsets</c> guarantees that
    /// every reference string in a well-formed buffer points to a real string
    /// literal (StrL1/2/4, encoded-length string, dictionary-encoded string).
    /// On a hostile / corrupted buffer a reference can
    /// point at another reference, which without a guard would let an
    /// attacker construct an arbitrarily deep chain or a cycle that drives
    /// the recursive re-writer into an unrecoverable
    /// <see cref="StackOverflowException"/>. The guard rejects these cases at
    /// the precise byte where the malformation occurs with a catchable
    /// <see cref="JsonInvalidTokenException"/>.
    /// </summary>
    [TestClass]
    public class JsonBinaryReferenceStringTests
    {
        private const byte BinaryFormatMarker = 0x80;
        private const byte ArrL1TypeMarker = 0xE2;
        private const byte Arr0TypeMarker = 0xE0;
        private const byte Arr1TypeMarker = 0xE1;
        private const byte StrL1TypeMarker = 0xC0;
        private const byte StrR1TypeMarker = 0xC3;
        private const byte StrR2TypeMarker = 0xC4;
        private const byte StrR4TypeMarker = 0xC6;

        [TestMethod]
        [Owner("tvaron")]
        public void ValidStrR1OneHopRewriteSucceeds()
        {
            // Sanity test: a well-formed reference string still rewrites
            // successfully. Layout:
            //   [0] 0x80                  binary format
            //   [1] ArrL1                 array with 1-byte length
            //   [2] 9                     array body length (9 bytes from [3]..[11])
            //   [3] StrL1                 1-byte length string
            //   [4] 5                     length = 5
            //   [5..9] 'h','e','l','l','o'
            //   [10] StrR1                reference string
            //   [11] 3                    offset = 3 (points to StrL1 at [3])
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                9,
                StrL1TypeMarker,
                5, (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o',
                StrR1TypeMarker,
                3,
            };

            ReadOnlyMemory<byte> rewritten = RewriteBinaryPayload(payload);

            // The rewriter is allowed to expand the reference into a literal
            // string. We assert that the rewrite succeeded (non-empty result
            // starting with the binary format marker) and that the resulting
            // buffer parses back into the same logical document.
            Assert.IsTrue(rewritten.Length > 0);
            Assert.AreEqual(BinaryFormatMarker, rewritten.Span[0]);

            CosmosArray roundTripped = CosmosElement.CreateFromBuffer<CosmosArray>(rewritten);
            Assert.AreEqual(2, roundTripped.Count);
            Assert.AreEqual("hello", ((CosmosString)roundTripped[0]).Value.ToString());
            Assert.AreEqual("hello", ((CosmosString)roundTripped[1]).Value.ToString());
        }

        [TestMethod]
        [Owner("tvaron")]
        public void StrR1RedirectingToStrR1IsRejected()
        {
            // Linear two-hop chain: StrR1 -> StrR1 -> StrL1. The writer never
            // emits reference-to-reference, so this is malformed. Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 7           body length (3 + 2 + 2)
            //   [3] StrL1
            //   [4] 1
            //   [5] 'x'
            //   [6] StrR1
            //   [7] 8           offset = 8 -> another StrR1 (the redirect)
            //   [8] StrR1
            //   [9] 3           offset = 3 -> the StrL1
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                7,
                StrL1TypeMarker,
                1, (byte)'x',
                StrR1TypeMarker, 8,
                StrR1TypeMarker, 3,
            };

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => RewriteBinaryPayload(payload));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void StrR1SelfReferenceIsRejected()
        {
            // Pathological self-reference: a StrR1 whose offset points to
            // itself. Without the guard this would be an infinite recursion
            // and uncatchable StackOverflowException. Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 2           body length
            //   [3] StrR1
            //   [4] 3           offset = 3 (self)
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                2,
                StrR1TypeMarker, 3,
            };

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => RewriteBinaryPayload(payload));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void DeepStrR1ChainIsRejected()
        {
            // The original DoS shape, adapted to the reference-string path:
            // a long chain of StrR1 markers, each pointing to the next, and
            // the final one to a real string literal. Even though the chain
            // would otherwise blow the stack, the guard fires at the very
            // first hop because StrR1 -> StrR1 is rejected.
            const int chainLength = 100;

            int bodyLength = (chainLength * 2) + 3; // 3 bytes for the final StrL1 + len + char
            Assert.IsTrue(bodyLength <= 0xFF, "Test payload too large for ArrL1 body length byte.");

            byte[] payload = new byte[3 + bodyLength];
            payload[0] = BinaryFormatMarker;
            payload[1] = ArrL1TypeMarker;
            payload[2] = (byte)bodyLength;

            int strLOffset = 3 + (chainLength * 2);
            for (int i = 0; i < chainLength; i++)
            {
                int markerOffset = 3 + (i * 2);
                payload[markerOffset] = StrR1TypeMarker;
                payload[markerOffset + 1] = (byte)(markerOffset + 2);
            }

            payload[strLOffset] = StrL1TypeMarker;
            payload[strLOffset + 1] = 1;
            payload[strLOffset + 2] = (byte)'x';

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => RewriteBinaryPayload(payload));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void StrR1OffsetOutOfBoundsIsRejected()
        {
            // The StrR1 target offset is past the end of the buffer. Without
            // the bounds check the dereference would either AV or read into
            // unrelated memory.
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                2,
                StrR1TypeMarker, 0xFE,
            };

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => RewriteBinaryPayload(payload));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void StrR2RedirectingToStrR2IsRejected()
        {
            // 2-byte reference variant. Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 10          body length
            //   [3] StrL1
            //   [4] 1
            //   [5] 'x'
            //   [6] StrR2
            //   [7..8] 9, 0     little-endian ushort offset = 9 (another StrR2)
            //   [9] StrR2
            //   [10..11] 3, 0   little-endian ushort offset = 3 (StrL1)
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                9,
                StrL1TypeMarker,
                1, (byte)'x',
                StrR2TypeMarker, 9, 0,
                StrR2TypeMarker, 3, 0,
            };

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => RewriteBinaryPayload(payload));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void StrR4RedirectingToStrR4IsRejected()
        {
            // 4-byte reference variant. Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 13          body length
            //   [3] StrL1
            //   [4] 1
            //   [5] 'x'
            //   [6] StrR4
            //   [7..10] 11, 0, 0, 0    little-endian int offset = 11 (another StrR4)
            //   [11] StrR4
            //   [12..15] 3, 0, 0, 0    offset = 3 (StrL1)
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                13,
                StrL1TypeMarker,
                1, (byte)'x',
                StrR4TypeMarker, 11, 0, 0, 0,
                StrR4TypeMarker, 3, 0, 0, 0,
            };

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => RewriteBinaryPayload(payload));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void StrR4NegativeOffsetIsRejected()
        {
            // StrR4 reads a signed 32-bit offset. The bounds check in
            // RewriteResolvedReferenceString uses an unsigned compare so
            // negative offsets are treated as out-of-range and rejected
            // instead of indexing before the start of the root buffer.
            // Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 5            body length
            //   [3] StrR4
            //   [4..7] 0xFF,0xFF,0xFF,0xFF   little-endian int = -1
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                5,
                StrR4TypeMarker, 0xFF, 0xFF, 0xFF, 0xFF,
            };

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => RewriteBinaryPayload(payload));
        }

        [TestMethod]
        [Owner("tvaron")]
        public void StrR1RedirectingToArrayMarkerIsRejected()
        {
            // The writer invariant (FixReferenceStringOffsets) guarantees that
            // an StrR offset always resolves to a string literal. A hostile
            // buffer that aims the offset at a non-string marker (here: ArrL1)
            // would otherwise fall through to ForceRewriteRawJsonValue's array
            // branch and start walking the buffer as if it were an array body.
            // The IsString check in RewriteResolvedReferenceString rejects it
            // up front. Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 4            body length (3 + 1, but the trailing StrR1
            //                    points back to byte [1] which is ArrL1)
            //   [3] StrL1
            //   [4] 0            empty string literal so the array is valid
            //   [5] StrR1
            //   [6] 1            offset = 1 -> ArrL1 marker (not a string)
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                4,
                StrL1TypeMarker, 0,
                StrR1TypeMarker, 1,
            };

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => RewriteBinaryPayload(payload));
        }

        /// <summary>
        /// Drives a hand-crafted binary payload through the
        /// <c>WriteRawJsonValue</c> path that exercises
        /// <c>ForceRewriteRawJsonValue</c>. Uses the binary navigator's
        /// <c>WriteNode</c> on the root node so the same-format fast-path is
        /// taken (the only path that calls into the recursive rewriter).
        /// </summary>
        private static ReadOnlyMemory<byte> RewriteBinaryPayload(byte[] payload)
        {
            IJsonNavigator navigator = JsonNavigator.Create(payload);
            IJsonWriter writer = JsonWriter.Create(JsonSerializationFormat.Binary);
            navigator.WriteNode(navigator.GetRootNode(), writer);
            return writer.GetResult();
        }
    }
}
