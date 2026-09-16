//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.Data.Encryption.Cryptography.Serializers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class V4CompressionEnvelopeTests
    {
        private static readonly Func<int, int> EncryptByteCount =
            length => 49 + (((length / 16) + 1) * 16);

        [DataTestMethod]
        [DataRow((byte)CompressionCodecId.Raw)]
        [DataRow((byte)CompressionCodecId.Deflate)]
        [DataRow((byte)CompressionCodecId.Brotli)]
        public void CreateAndDecode_RoundTrips(byte codecValue)
        {
            CompressionCodecId codec = (CompressionCodecId)codecValue;
            byte[] payload = Encoding.UTF8.GetBytes(new string('a', 4096));
            CompressionSettings settings = new (
                codec,
                CompressionLevelSetting.Balanced,
                minimumContentLength: 0);

            using ArrayPoolManager pool = new ();
            (byte[] envelope, int envelopeLength) = V4CompressionEnvelope.Create(
                payload,
                TypeMarker.String,
                settings,
                EncryptByteCount,
                pool);

            DecodedEnvelope decoded = V4CompressionEnvelope.Decode(
                envelope,
                envelopeLength,
                (byte)TypeMarker.String,
                new DecompressionBudget(),
                pool);

            Assert.AreEqual(TypeMarker.String, decoded.TypeMarker);
            CollectionAssert.AreEqual(payload, decoded.Buffer.AsSpan(decoded.Offset, decoded.Length).ToArray());
        }

        [TestMethod]
        public void CreateAndDecode_AllEmittedTypeMarkersRoundTrip()
        {
            using ArrayPoolManager pool = new ();
            CompressionSettings settings = new (
                CompressionCodecId.Raw,
                CompressionLevelSetting.Balanced,
                minimumContentLength: 0);

            foreach ((TypeMarker TypeMarker, byte[] Payload) value in new[]
            {
                (TypeMarker.String, Encoding.UTF8.GetBytes("value")),
                (TypeMarker.Double, new byte[8]),
                (TypeMarker.Long, new byte[8]),
                (TypeMarker.Boolean, new byte[8]),
                (TypeMarker.Array, Encoding.UTF8.GetBytes("[]")),
                (TypeMarker.Object, Encoding.UTF8.GetBytes("{}")),
            })
            {
                (byte[] envelope, int envelopeLength) = V4CompressionEnvelope.Create(
                    value.Payload,
                    value.TypeMarker,
                    settings,
                    EncryptByteCount,
                    pool);

                DecodedEnvelope decoded = V4CompressionEnvelope.Decode(
                    envelope,
                    envelopeLength,
                    (byte)value.TypeMarker,
                    new DecompressionBudget(),
                    pool);

                Assert.AreEqual(value.TypeMarker, decoded.TypeMarker);
                CollectionAssert.AreEqual(
                    value.Payload,
                    decoded.Buffer.AsSpan(decoded.Offset, decoded.Length).ToArray());
            }
        }

        [TestMethod]
        public void Create_UsesRawForEmptyBelowThresholdAndNonBeneficialCompression()
        {
            using ArrayPoolManager pool = new ();

            AssertRaw(
                V4CompressionEnvelope.Create(
                    ReadOnlySpan<byte>.Empty,
                    TypeMarker.String,
                    new CompressionSettings(CompressionCodecId.Deflate, CompressionLevelSetting.Fastest, 0),
                    EncryptByteCount,
                    pool));

            AssertRaw(
                V4CompressionEnvelope.Create(
                    Encoding.UTF8.GetBytes(new string('a', 255)),
                    TypeMarker.String,
                    new CompressionSettings(CompressionCodecId.Deflate, CompressionLevelSetting.Fastest, 256),
                    EncryptByteCount,
                    pool));

            byte[] random = new byte[300];
            new Random(42).NextBytes(random);
            AssertRaw(
                V4CompressionEnvelope.Create(
                    random,
                    TypeMarker.String,
                    new CompressionSettings(CompressionCodecId.Brotli, CompressionLevelSetting.Balanced, 0),
                    EncryptByteCount,
                    pool));
        }

        [TestMethod]
        public void CompressionSettings_RejectsInvalidValues()
        {
            Assert.ThrowsException<ArgumentException>(() => new CompressionSettings(
                (CompressionCodecId)3,
                CompressionLevelSetting.Balanced,
                0));
            Assert.ThrowsException<ArgumentException>(() => new CompressionSettings(
                CompressionCodecId.Deflate,
                (CompressionLevelSetting)255,
                0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CompressionSettings(
                CompressionCodecId.Deflate,
                CompressionLevelSetting.Balanced,
                -1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CompressionSettings(
                CompressionCodecId.Deflate,
                CompressionLevelSetting.Balanced,
                (64 * 1024 * 1024) + 1));
        }

        [TestMethod]
        public void Decode_RejectsOuterTypeMismatchUnknownCodecAndTrailingBytes()
        {
            byte[] payload = Encoding.UTF8.GetBytes(new string('b', 1024));
            using ArrayPoolManager pool = new ();
            (byte[] envelope, int envelopeLength) = V4CompressionEnvelope.Create(
                payload,
                TypeMarker.String,
                new CompressionSettings(CompressionCodecId.Brotli, CompressionLevelSetting.Balanced, 0),
                EncryptByteCount,
                pool);

            InvalidOperationException mismatch = Assert.ThrowsException<InvalidOperationException>(() =>
                V4CompressionEnvelope.Decode(
                    envelope,
                    envelopeLength,
                    (byte)TypeMarker.Long,
                    new DecompressionBudget(),
                    pool));
            Assert.AreEqual(
                "Envelope inner type marker does not match the outer type marker.",
                mismatch.Message);

            byte[] unknownCodec = new byte[EnvelopeHeaderConstants.HeaderSize + 1];
            EnvelopeHeaderWriter.Write(unknownCodec, (byte)TypeMarker.String, CompressionCodecId.Raw);
            unknownCodec[EnvelopeHeaderConstants.CodecOffset] = 3;
            unknownCodec[EnvelopeHeaderConstants.HeaderSize] = 1;
            Assert.ThrowsException<InvalidOperationException>(() => V4CompressionEnvelope.Decode(
                unknownCodec,
                unknownCodec.Length,
                (byte)TypeMarker.String,
                new DecompressionBudget(),
                pool));

            byte[] withTrailingByte = new byte[envelopeLength + 1];
            envelope.AsSpan(0, envelopeLength).CopyTo(withTrailingByte);
            Assert.ThrowsException<InvalidOperationException>(() => V4CompressionEnvelope.Decode(
                withTrailingByte,
                withTrailingByte.Length,
                (byte)TypeMarker.String,
                new DecompressionBudget(),
                pool));
        }

        [TestMethod]
        public void Decode_RejectsMalformedLengthsCorruptPayloadAndBudgetOverflow()
        {
            using ArrayPoolManager pool = new ();

            AssertInvalid(CreateEnvelope(CompressionCodecId.Deflate, new byte[] { 0x80, 0x00 }, new byte[] { 1 }, new byte[] { 1 }), pool);
            AssertInvalid(CreateEnvelope(CompressionCodecId.Deflate, new byte[] { 1 }, new byte[] { 0x80 }, new byte[] { 1 }), pool);
            AssertInvalid(CreateEnvelope(CompressionCodecId.Deflate, 0, 1, new byte[] { 1 }), pool);
            AssertInvalid(CreateEnvelope(CompressionCodecId.Deflate, (64 * 1024 * 1024) + 1U, 1, new byte[] { 1 }), pool);
            AssertInvalid(CreateEnvelope(CompressionCodecId.Deflate, 1, 0, Array.Empty<byte>()), pool);
            AssertInvalid(CreateEnvelope(CompressionCodecId.Deflate, 1, (64 * 1024 * 1024) + 1U, new byte[] { 1 }), pool);
            AssertInvalid(CreateEnvelope(CompressionCodecId.Deflate, 1, 2, new byte[] { 1 }), pool);

            Assert.ThrowsException<InvalidDataException>(() => V4CompressionEnvelope.Decode(
                CreateEnvelope(CompressionCodecId.Deflate, 4, 3, new byte[] { 1, 2, 3 }),
                EnvelopeHeaderConstants.HeaderSize + 2 + 3,
                (byte)TypeMarker.String,
                new DecompressionBudget(),
                pool));

            byte[] raw = CreateRawEnvelope(new byte[] { 1, 2 }, TypeMarker.String);
            Assert.ThrowsException<InvalidOperationException>(() => V4CompressionEnvelope.Decode(
                raw,
                raw.Length,
                (byte)TypeMarker.String,
                new DecompressionBudget(1),
                pool));
        }

        [TestMethod]
        public void Decode_CompressedPayloadChargesBudgetBeforeDecompression()
        {
            byte[] envelope = CreateEnvelope(
                CompressionCodecId.Deflate,
                rawLength: 1024,
                compressedLength: 3,
                compressedBody: new byte[] { 1, 2, 3 });
            using ArrayPoolManager pool = new ();

            Assert.ThrowsException<InvalidDataException>(() => V4CompressionEnvelope.Decode(
                envelope,
                envelope.Length,
                (byte)TypeMarker.String,
                new DecompressionBudget(),
                pool));

            using ArrayPoolManager budgetPool = new ();
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
                V4CompressionEnvelope.Decode(
                    envelope,
                    envelope.Length,
                    (byte)TypeMarker.String,
                    new DecompressionBudget(1023),
                    budgetPool));

            Assert.AreEqual(
                "Aggregate decompressed content exceeds the request budget.",
                exception.Message);
            Assert.AreEqual(0, GetRentCount(budgetPool));
        }

        [TestMethod]
        public void StructuralProbe_RawLengthMatrixUsesAuthenticatedInnerType()
        {
            foreach ((TypeMarker TypeMarker, int RawLength) valid in new[]
            {
                (TypeMarker.String, 0),
                (TypeMarker.String, 1),
                (TypeMarker.Long, 8),
                (TypeMarker.Double, 8),
                (TypeMarker.Boolean, 8),
                (TypeMarker.Array, 2),
                (TypeMarker.Object, 2),
            })
            {
                Assert.IsTrue(V4CompressionEnvelope.IsStructurallyValidV4Envelope(
                    CreateRawEnvelope(new byte[valid.RawLength], valid.TypeMarker)));
            }

            foreach ((TypeMarker TypeMarker, int RawLength) invalid in new[]
            {
                (TypeMarker.Long, 7),
                (TypeMarker.Long, 9),
                (TypeMarker.Double, 7),
                (TypeMarker.Double, 9),
                (TypeMarker.Boolean, 7),
                (TypeMarker.Boolean, 9),
                (TypeMarker.Array, 0),
                (TypeMarker.Array, 1),
                (TypeMarker.Object, 0),
                (TypeMarker.Object, 1),
            })
            {
                Assert.IsFalse(V4CompressionEnvelope.IsStructurallyValidV4Envelope(
                    CreateRawEnvelope(new byte[invalid.RawLength], invalid.TypeMarker)));
            }

            byte[] nullEnvelope = CreateRawEnvelope(Array.Empty<byte>(), TypeMarker.String);
            nullEnvelope[EnvelopeHeaderConstants.InnerTypeOffset] = (byte)TypeMarker.Null;
            AssertRejectedByAllReaders(nullEnvelope, (byte)TypeMarker.Null);

            byte[] oversizedString = new byte[
                EnvelopeHeaderConstants.HeaderSize +
                (64 * 1024 * 1024) +
                1];
            EnvelopeHeaderWriter.Write(
                oversizedString,
                (byte)TypeMarker.String,
                CompressionCodecId.Raw);
            Assert.IsFalse(V4CompressionEnvelope.IsStructurallyValidV4Envelope(
                oversizedString));

            byte hypotheticalOuterTypeMarker = (byte)TypeMarker.Long;
            Assert.AreNotEqual((byte)TypeMarker.String, hypotheticalOuterTypeMarker);
            Assert.IsTrue(V4CompressionEnvelope.IsStructurallyValidV4Envelope(
                CreateRawEnvelope(new byte[1], TypeMarker.String)));
        }

        [TestMethod]
        public void StructuralProbe_CompressedCanonicalFramingMatrix()
        {
            foreach (CompressionCodecId codec in new[] { CompressionCodecId.Deflate, CompressionCodecId.Brotli })
            {
                foreach ((TypeMarker TypeMarker, uint RawLength) valid in new[]
                {
                    (TypeMarker.String, 1U),
                    (TypeMarker.Long, 8U),
                    (TypeMarker.Double, 8U),
                    (TypeMarker.Boolean, 8U),
                    (TypeMarker.Array, 2U),
                    (TypeMarker.Object, 2U),
                })
                {
                    Assert.IsTrue(V4CompressionEnvelope.IsStructurallyValidV4Envelope(
                        CreateEnvelope(
                            codec,
                            valid.RawLength,
                            1,
                            new byte[] { 0x01 },
                            valid.TypeMarker)));
                }

                foreach (byte[] invalid in new[]
                {
                    CreateEnvelope(codec, 0, 1, new byte[] { 0x01 }),
                    CreateEnvelope(codec, 1, 0, Array.Empty<byte>()),
                    CreateEnvelope(
                        codec,
                        (64 * 1024 * 1024) + 1U,
                        1,
                        new byte[] { 0x01 }),
                    CreateEnvelope(
                        codec,
                        new byte[] { 0x81, 0x00 },
                        new byte[] { 0x01 },
                        new byte[] { 0x01 }),
                    CreateEnvelope(
                        codec,
                        new byte[] { 0x01 },
                        new byte[] { 0x81, 0x00 },
                        new byte[] { 0x01 }),
                    CreateEnvelope(
                        codec,
                        new byte[] { 0x80 },
                        Array.Empty<byte>(),
                        Array.Empty<byte>()),
                    CreateEnvelope(
                        codec,
                        new byte[] { 0x01 },
                        new byte[] { 0x80 },
                        Array.Empty<byte>()),
                    CreateEnvelope(
                        codec,
                        new byte[] { 0x01 },
                        Encode((64 * 1024 * 1024) + 1U),
                        new byte[] { 0x01 }),
                    CreateEnvelope(codec, 7, 1, new byte[] { 0x01 }, TypeMarker.Long),
                    CreateEnvelope(
                        codec,
                        new byte[] { 0x01 },
                        new byte[] { 0x02 },
                        new byte[] { 0x01 }),
                    CreateEnvelope(
                        codec,
                        new byte[] { 0x01 },
                        new byte[] { 0x01 },
                        new byte[] { 0x01, 0x02 }),
                })
                {
                    Assert.IsFalse(V4CompressionEnvelope.IsStructurallyValidV4Envelope(invalid));
                }
            }

            for (int length = 0; length < EnvelopeHeaderConstants.HeaderSize; length++)
            {
                Assert.IsFalse(V4CompressionEnvelope.IsStructurallyValidV4Envelope(
                    new byte[length]));
            }
        }

        [TestMethod]
        public void StructuralProbe_RejectsLegacyLongAndDoubleEightByteCollisions()
        {
            Assert.IsFalse(V4CompressionEnvelope.IsStructurallyValidV4Envelope(
                new byte[] { 0x43, 0x45, 0xC5, 0x01, 0x04, 0x04, 0x00, 0x00 }));
            Assert.IsFalse(V4CompressionEnvelope.IsStructurallyValidV4Envelope(
                new byte[] { 0x43, 0x45, 0xC5, 0x01, 0x04, 0x03, 0x00, 0x00 }));
            Assert.IsFalse(
                V4CompressionEnvelope.IsStructurallyValidV4Envelope(
                    new byte[] { 0x43, 0x45, 0xC5, 0x01, 0x04, 0x02, 0x00, (byte)'x' }),
                "The malformed old seven-byte header must be rejected.");
        }

        [TestMethod]
        public void MalformedOldSevenByteHeaders_AreRejectedByHeaderReaderStructuralProbeAndDecode()
        {
            AssertRejectedByAllReaders(
                new byte[] { 0x43, 0x45, 0xC5, 0x01, 0x04, 0x02, 0x00, 0x56, 0x34, 0x78 },
                (byte)TypeMarker.String);

            for (byte innerType = EnvelopeHeaderConstants.MinValidInnerTypeMarker;
                innerType <= EnvelopeHeaderConstants.MaxValidInnerTypeMarker;
                innerType++)
            {
                for (byte codec = (byte)CompressionCodecId.Raw;
                    codec <= (byte)CompressionCodecId.Brotli;
                    codec++)
                {
                    AssertRejectedByAllReaders(
                        new byte[] { 0x43, 0x45, 0xC5, 0x01, 0x04, innerType, codec, 0xAA, 0xBB, 0xCC },
                        innerType);
                }
            }
        }

        [TestMethod]
        public void StructuralProbe_NeverThrowsOrAllocatesForMalformedInput()
        {
            byte[] valid = CreateEnvelope(
                CompressionCodecId.Deflate,
                1,
                1,
                new byte[] { 0x01 });
            for (int length = 0; length <= valid.Length; length++)
            {
                _ = V4CompressionEnvelope.IsStructurallyValidV4Envelope(
                    valid.AsSpan(0, length));
            }

            Random random = new (42);
            for (int length = 0; length < 256; length++)
            {
                byte[] malformed = new byte[length];
                random.NextBytes(malformed);
                _ = V4CompressionEnvelope.IsStructurallyValidV4Envelope(malformed);
            }

            _ = V4CompressionEnvelope.IsStructurallyValidV4Envelope(valid);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            bool allValid = true;
            for (int i = 0; i < 1000; i++)
            {
                allValid &= V4CompressionEnvelope.IsStructurallyValidV4Envelope(valid);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.IsTrue(allValid);
            Assert.AreEqual(0L, allocated);
        }

        [TestMethod]
        public void StructuralParser_RejectsImplausibleRawFrameInProbeAndDecode()
        {
            byte[] invalid = CreateRawEnvelope(new byte[1], TypeMarker.Boolean);
            Assert.IsFalse(V4CompressionEnvelope.IsStructurallyValidV4Envelope(invalid));

            using ArrayPoolManager pool = new ();
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
                V4CompressionEnvelope.Decode(
                    invalid,
                    invalid.Length,
                    (byte)TypeMarker.Boolean,
                    new DecompressionBudget(),
                    pool));
            Assert.AreEqual(
                "Encrypted envelope failed integrity validation.",
                exception.Message);
        }

        [TestMethod]
        public void StructuralProbe_BooleanLengthMatchesSqlSerializer()
        {
            SqlBitSerializer serializer = new ();
            byte[] buffer = new byte[serializer.GetSerializedMaxByteCount()];

            Assert.AreEqual(8, serializer.Serialize(true, buffer));
            Assert.AreEqual(8, serializer.Serialize(false, buffer));
            Assert.IsTrue(V4CompressionEnvelope.IsStructurallyValidV4Envelope(
                CreateRawEnvelope(buffer, TypeMarker.Boolean)));
        }

        private static void AssertRaw((byte[] Buffer, int Length) envelope)
        {
            Assert.IsTrue(EnvelopeHeaderReader.TryReadV4Header(
                envelope.Buffer.AsSpan(0, envelope.Length),
                out EnvelopeHeader header,
                out _));
            Assert.AreEqual(CompressionCodecId.Raw, header.Codec);
        }

        private static byte[] CreateRawEnvelope(byte[] body, TypeMarker typeMarker)
        {
            byte[] envelope = new byte[EnvelopeHeaderConstants.HeaderSize + body.Length];
            EnvelopeHeaderWriter.Write(envelope, (byte)typeMarker, CompressionCodecId.Raw);
            body.CopyTo(envelope, EnvelopeHeaderConstants.HeaderSize);
            return envelope;
        }

        private static byte[] CreateEnvelope(
            CompressionCodecId codec,
            uint rawLength,
            uint compressedLength,
            byte[] compressedBody,
            TypeMarker typeMarker = TypeMarker.String)
        {
            return CreateEnvelope(codec, Encode(rawLength), Encode(compressedLength), compressedBody, typeMarker);
        }

        private static byte[] CreateEnvelope(
            CompressionCodecId codec,
            byte[] rawLengthBytes,
            byte[] compressedLengthBytes,
            byte[] compressedBody,
            TypeMarker typeMarker = TypeMarker.String)
        {
            byte[] envelope = new byte[
                EnvelopeHeaderConstants.HeaderSize +
                rawLengthBytes.Length +
                compressedLengthBytes.Length +
                compressedBody.Length];
            EnvelopeHeaderWriter.Write(envelope, (byte)typeMarker, codec);
            int offset = EnvelopeHeaderConstants.HeaderSize;
            rawLengthBytes.CopyTo(envelope, offset);
            offset += rawLengthBytes.Length;
            compressedLengthBytes.CopyTo(envelope, offset);
            offset += compressedLengthBytes.Length;
            compressedBody.CopyTo(envelope, offset);
            return envelope;
        }

        private static byte[] Encode(uint value)
        {
            byte[] buffer = new byte[4];
            int length = UleB128.Write(value, buffer);
            return buffer.AsSpan(0, length).ToArray();
        }

        private static int GetRentCount(ArrayPoolManager pool)
        {
            FieldInfo rentedBuffers = typeof(ArrayPoolManager<byte>).GetField(
                "rentedBuffers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return ((List<byte[]>)rentedBuffers.GetValue(pool)).Count;
        }

        private static void AssertRejectedByAllReaders(byte[] envelope, byte outerType)
        {
            Assert.IsFalse(EnvelopeHeaderReader.TryReadV4Header(envelope, out _, out _));
            Assert.IsFalse(V4CompressionEnvelope.IsStructurallyValidV4Envelope(envelope));

            using ArrayPoolManager pool = new ();
            Assert.ThrowsException<InvalidOperationException>(() => V4CompressionEnvelope.Decode(
                envelope,
                envelope.Length,
                outerType,
                new DecompressionBudget(),
                pool));
        }

        private static void AssertInvalid(byte[] envelope, ArrayPoolManager pool)
        {
            Assert.ThrowsException<InvalidOperationException>(() => V4CompressionEnvelope.Decode(
                envelope,
                envelope.Length,
                (byte)TypeMarker.String,
                new DecompressionBudget(),
                pool));
        }
    }
}
#endif
