//-----------------------------------------------------------------------
// <copyright file="JsonBinaryReaderReferenceStringTests.cs" company="Microsoft Corporation">
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
    /// Regression tests for the reference-string (StrR1/2/3/4) dereference
    /// guard on the binary <b>reader / navigator / CosmosElement</b>
    /// value-materialization path
    /// (<c>JsonBinaryEncoding.TryGetReferenceStringTarget</c>).
    ///
    /// This is the reader-side counterpart of
    /// <see cref="JsonBinaryReferenceStringTests"/>, which only covered the
    /// binary <b>writer</b> re-serializer fixed by PR #5909. The writer's
    /// <c>FixReferenceStringOffsets</c> guarantees that every reference string
    /// in a well-formed buffer resolves to a real string literal in exactly
    /// one hop. A hostile / corrupted buffer can point a reference at another
    /// reference (or at itself), which without a guard drives the recursive
    /// string decode in <c>JsonBinaryEncoding.TryGetBufferedStringValue</c>
    /// into an unrecoverable, process-crashing
    /// <see cref="StackOverflowException"/>. The guard rejects these cases
    /// with a catchable <see cref="JsonInvalidTokenException"/>, matching the
    /// writer contract.
    ///
    /// NOTE: none of these payloads can be allowed to crash the test host, so
    /// every test asserts the <b>post-fix</b> catchable exception form. The
    /// pre-fix behavior was either a StackOverflow (self / cyclic references)
    /// or a silent wrong answer (reference-to-reference).
    /// </summary>
    [TestClass]
    public class JsonBinaryReaderReferenceStringTests
    {
        private const byte BinaryFormatMarker = 0x80;
        private const byte ArrL1TypeMarker = 0xE2;
        private const byte StrL1TypeMarker = 0xC0;
        private const byte StrR1TypeMarker = 0xC3;
        private const byte StrR2TypeMarker = 0xC4;
        private const byte StrR3TypeMarker = 0xC5;
        private const byte StrR4TypeMarker = 0xC6;

        [TestMethod]
        [Owner("nalutripician")]
        public void StrR1SelfReference_Reader_IsRejected()
        {
            // Core DoS regression. A StrR1 whose offset points at itself.
            // Pre-fix this recursed forever -> StackOverflowException (crash);
            // post-fix it is rejected with JsonInvalidTokenException. Layout:
            //   [0] 0x80           binary format
            //   [1] ArrL1          array, 1-byte length
            //   [2] 2              body length
            //   [3] StrR1
            //   [4] 3              offset = 3 (self)
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                2,
                StrR1TypeMarker, 3,
            };

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadStringElementViaCosmos(payload, index: 0));
            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadAllStringsViaJsonReader(payload));
        }

        [TestMethod]
        [Owner("nalutripician")]
        public void StrR1RedirectingToStrR1_Reader_IsRejected()
        {
            // Reference-to-reference: StrR1 -> StrR1 -> StrL1 "x". This is the
            // exact payload the WRITER test rejects; pre-fix the READER
            // silently followed the chain and returned "x". Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 7
            //   [3] StrL1
            //   [4] 1
            //   [5] 'x'
            //   [6] StrR1, [7] 8   offset = 8 -> another StrR1
            //   [8] StrR1, [9] 3   offset = 3 -> the StrL1
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

            // element[0] is the StrL1 "x" and reads fine; element[1] is the
            // malformed reference-to-reference and must be rejected.
            Assert.AreEqual("x", ReadStringElementViaCosmos(payload, index: 0));
            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadStringElementViaCosmos(payload, index: 1));
            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadAllStringsViaJsonReader(payload));
        }

        [TestMethod]
        [Owner("nalutripician")]
        public void StrR2RedirectingToStrR2_Reader_IsRejected()
        {
            // 2-byte reference variant: StrR2 -> StrR2 -> StrL1 "x". Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 9
            //   [3] StrL1, [4] 1, [5] 'x'
            //   [6] StrR2, [7..8] 9,0   offset = 9 -> another StrR2
            //   [9] StrR2, [10..11] 3,0 offset = 3 -> the StrL1
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
                () => ReadStringElementViaCosmos(payload, index: 1));
            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadAllStringsViaJsonReader(payload));
        }

        [TestMethod]
        [Owner("nalutripician")]
        public void StrR3RedirectingToStrR3_Reader_IsRejected()
        {
            // 3-byte reference variant: StrR3 -> StrR3 -> StrL1 "x". Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 11
            //   [3] StrL1, [4] 1, [5] 'x'
            //   [6] StrR3, [7..9] 10,0,0   24-bit offset = 10 -> another StrR3
            //   [10] StrR3, [11..13] 3,0,0 24-bit offset = 3 -> the StrL1
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                11,
                StrL1TypeMarker,
                1, (byte)'x',
                StrR3TypeMarker, 10, 0, 0,
                StrR3TypeMarker, 3, 0, 0,
            };

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadStringElementViaCosmos(payload, index: 1));
            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadAllStringsViaJsonReader(payload));
        }

        [TestMethod]
        [Owner("nalutripician")]
        public void StrR4RedirectingToStrR4_Reader_IsRejected()
        {
            // 4-byte reference variant: StrR4 -> StrR4 -> StrL1 "x". Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 13
            //   [3] StrL1, [4] 1, [5] 'x'
            //   [6] StrR4, [7..10] 11,0,0,0   offset = 11 -> another StrR4
            //   [11] StrR4, [12..15] 3,0,0,0  offset = 3 -> the StrL1
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
                () => ReadStringElementViaCosmos(payload, index: 1));
            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadAllStringsViaJsonReader(payload));
        }

        [TestMethod]
        [Owner("nalutripician")]
        public void StrR1OffsetOutOfBounds_Reader_IsRejected()
        {
            // The StrR1 target offset is past the end of the buffer. Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 2
            //   [3] StrR1, [4] 0xFE   offset = 254 (out of range)
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                2,
                StrR1TypeMarker, 0xFE,
            };

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadStringElementViaCosmos(payload, index: 0));
            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadAllStringsViaJsonReader(payload));
        }

        [TestMethod]
        [Owner("nalutripician")]
        public void StrR4NegativeOffset_Reader_IsRejected()
        {
            // StrR4 reads a signed 32-bit offset. The guard uses an unsigned
            // compare so a negative offset is treated as out-of-range and
            // rejected instead of indexing before the start of the buffer.
            // Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 5
            //   [3] StrR4, [4..7] 0xFF,0xFF,0xFF,0xFF   little-endian int = -1
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                5,
                StrR4TypeMarker, 0xFF, 0xFF, 0xFF, 0xFF,
            };

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadStringElementViaCosmos(payload, index: 0));
            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadAllStringsViaJsonReader(payload));
        }

        [TestMethod]
        [Owner("nalutripician")]
        public void StrR1RedirectingToNonStringMarker_Reader_IsRejected()
        {
            // The writer invariant guarantees an StrR offset always resolves to
            // a string literal. A hostile buffer that aims the offset at a
            // non-string marker (here: the ArrL1 marker at [1]) exercises the
            // !IsString branch of the guard and must be rejected. Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 4
            //   [3] StrL1, [4] 0   empty string literal so element[0] is valid
            //   [5] StrR1, [6] 1   offset = 1 -> ArrL1 marker (not a string)
            byte[] payload = new byte[]
            {
                BinaryFormatMarker,
                ArrL1TypeMarker,
                4,
                StrL1TypeMarker, 0,
                StrR1TypeMarker, 1,
            };

            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadStringElementViaCosmos(payload, index: 1));
            Assert.ThrowsException<JsonInvalidTokenException>(
                () => ReadAllStringsViaJsonReader(payload));
        }

        [TestMethod]
        [Owner("nalutripician")]
        public void ValidStrR1OneHop_Reader_StillResolves()
        {
            // Guard against over-rejection: a well-formed single-hop
            // StrR1 -> StrL1 must still resolve to the referenced literal.
            // Layout:
            //   [0] 0x80
            //   [1] ArrL1
            //   [2] 9
            //   [3] StrL1, [4] 5, [5..9] 'h','e','l','l','o'
            //   [10] StrR1, [11] 3   offset = 3 -> the StrL1
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

            // CosmosElement / navigator path.
            Assert.AreEqual("hello", ReadStringElementViaCosmos(payload, index: 0));
            Assert.AreEqual("hello", ReadStringElementViaCosmos(payload, index: 1));

            // JsonReader path.
            List<string> strings = ReadAllStringsViaJsonReader(payload);
            CollectionAssert.AreEqual(new[] { "hello", "hello" }, strings);
        }

        /// <summary>
        /// Materializes the string at <paramref name="index"/> of the binary
        /// array payload through the navigator / <see cref="CosmosElement"/>
        /// path, which dereferences reference strings via
        /// <c>JsonBinaryEncoding.TryGetBufferedStringValue</c>.
        /// </summary>
        private static string ReadStringElementViaCosmos(byte[] payload, int index)
        {
            CosmosArray array = CosmosElement.CreateFromBuffer<CosmosArray>(payload);
            return ((CosmosString)array[index]).Value.ToString();
        }

        /// <summary>
        /// Reads every string token in the payload through the
        /// <see cref="IJsonReader"/> path
        /// (<c>JsonReader.JsonBinaryReader.GetStringValue</c> ->
        /// <c>GetUtf8MemoryValue</c>), forcing each value to be materialized.
        /// </summary>
        private static List<string> ReadAllStringsViaJsonReader(byte[] payload)
        {
            IJsonReader reader = JsonReader.Create(payload);
            List<string> strings = new List<string>();
            while (reader.Read())
            {
                if (reader.CurrentTokenType == JsonTokenType.String)
                {
                    strings.Add(reader.GetStringValue().ToString());
                }
            }

            return strings;
        }
    }
}
