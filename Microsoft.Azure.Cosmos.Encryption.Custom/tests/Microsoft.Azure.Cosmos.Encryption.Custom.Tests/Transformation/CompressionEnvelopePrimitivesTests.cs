//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CompressionEnvelopePrimitivesTests
    {
        [TestMethod]
        public void EnvelopeHeader_WritesExactBytesAndRejectsTamper()
        {
            Assert.AreEqual(6, EnvelopeHeaderConstants.MagicLength);
            Assert.AreEqual(6, EnvelopeHeaderConstants.FormatEpochOffset);
            Assert.AreEqual(7, EnvelopeHeaderConstants.InnerTypeOffset);
            Assert.AreEqual(8, EnvelopeHeaderConstants.CodecOffset);
            Assert.AreEqual(9, EnvelopeHeaderConstants.HeaderSize);
            Span<byte> buffer = stackalloc byte[EnvelopeHeaderConstants.HeaderSize];
            EnvelopeHeaderWriter.Write(buffer, 7, CompressionCodecId.Brotli);

            CollectionAssert.AreEqual(
                new byte[] { 0x43, 0x45, 0xC5, 0x01, 0x56, 0x34, 0x04, 0x07, 0x02 },
                buffer.ToArray());

            Assert.IsTrue(EnvelopeHeaderReader.TryReadV4Header(buffer, out EnvelopeHeader header, out int consumed));
            Assert.AreEqual(EnvelopeHeaderConstants.HeaderSize, consumed);
            Assert.AreEqual(7, header.InnerType);
            Assert.AreEqual(CompressionCodecId.Brotli, header.Codec);

            byte[] nullMarker = buffer.ToArray();
            nullMarker[EnvelopeHeaderConstants.InnerTypeOffset] = (byte)TypeMarker.Null;
            Assert.IsFalse(EnvelopeHeaderReader.TryReadV4Header(nullMarker, out _, out _));

            for (int i = 0; i < EnvelopeHeaderConstants.HeaderSize; i++)
            {
                byte[] tampered = buffer.ToArray();
                tampered[i] ^= 0x7F;
                Assert.IsFalse(
                    EnvelopeHeaderReader.TryReadV4Header(tampered, out _, out _),
                    $"Tamper at offset {i} should fail.");
            }
        }

        [TestMethod]
        public void EnvelopeHeader_RejectsInvalidArgumentsAndUnknownCodec()
        {
            Assert.ThrowsException<ArgumentException>(() => EnvelopeHeaderWriter.Write(
                new byte[EnvelopeHeaderConstants.HeaderSize - 1],
                (byte)TypeMarker.String,
                CompressionCodecId.Raw));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => EnvelopeHeaderWriter.Write(
                new byte[EnvelopeHeaderConstants.HeaderSize],
                0,
                CompressionCodecId.Raw));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => EnvelopeHeaderWriter.Write(
                new byte[EnvelopeHeaderConstants.HeaderSize],
                (byte)TypeMarker.Null,
                CompressionCodecId.Raw));

            byte[] headerBuffer = new byte[EnvelopeHeaderConstants.HeaderSize];
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => EnvelopeHeaderWriter.Write(
                headerBuffer,
                (byte)TypeMarker.String,
                (CompressionCodecId)3));

            EnvelopeHeaderWriter.Write(headerBuffer, (byte)TypeMarker.String, CompressionCodecId.Raw);
            headerBuffer[EnvelopeHeaderConstants.CodecOffset] = 3;
            Assert.IsFalse(EnvelopeHeaderReader.TryReadV4Header(headerBuffer, out _, out _));
        }

        [TestMethod]
        public void CompressionCodecIds_AreStable()
        {
            Assert.AreEqual(0, (byte)CompressionCodecId.Raw);
            Assert.AreEqual(1, (byte)CompressionCodecId.Deflate);
            Assert.AreEqual(2, (byte)CompressionCodecId.Brotli);
            Assert.IsFalse(Enum.IsDefined(typeof(CompressionCodecId), (byte)3));
        }

        [TestMethod]
        public void UleB128_UsesCanonicalShortestEncoding()
        {
            byte[] scratch = new byte[4];
            for (int value = 0; value < 100000; value++)
            {
                int written = UleB128.Write((uint)value, scratch);
                Assert.IsTrue(UleB128.TryRead(scratch.AsSpan(0, written), out uint decoded, out int consumed));
                Assert.AreEqual((uint)value, decoded);
                Assert.AreEqual(written, consumed);
                Assert.AreEqual(written, UleB128.GetEncodedLength((uint)value));
            }

            foreach (uint value in new uint[] { 0x0FFFFFFF, 64 * 1024 * 1024 })
            {
                int written = UleB128.Write(value, scratch);
                Assert.IsTrue(UleB128.TryRead(scratch.AsSpan(0, written), out uint decoded, out int consumed));
                Assert.AreEqual(value, decoded);
                Assert.AreEqual(written, consumed);
            }
        }

        [TestMethod]
        public void UleB128_RejectsMalformedAndNonCanonicalEncoding()
        {
            Assert.IsFalse(UleB128.TryRead(new byte[] { 0x80 }, out _, out _));
            Assert.IsFalse(UleB128.TryRead(new byte[] { 0x80, 0x00 }, out _, out _));
            Assert.IsFalse(UleB128.TryRead(new byte[] { 0xAA, 0x80, 0x00 }, out _, out _));
            Assert.IsFalse(UleB128.TryRead(new byte[] { 0x80, 0x80, 0x80, 0x80 }, out _, out _));
            Assert.IsFalse(UleB128.TryRead(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x00 }, out _, out _));
            Assert.ThrowsException<ArgumentException>(() => UleB128.Write(0x80, new byte[1]));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => UleB128.GetEncodedLength(0x10000000));
        }

        [TestMethod]
        public void CompressionHelper_UsesExactEncryptedBase64BreakEven()
        {
            Func<int, int> encryptedBytes = plaintextLength => 49 + ((plaintextLength / 16) + 1) * 16;

            Assert.AreEqual(368, CompressionHelper.GetStoredBase64Length(209, encryptedBytes));
            Assert.AreEqual(260, CompressionHelper.GetStoredBase64Length(132, encryptedBytes));
            Assert.AreEqual(216, CompressionHelper.GetStoredBase64Length(109, encryptedBytes));
            Assert.AreEqual(216, CompressionHelper.GetStoredBase64Length(110, encryptedBytes));

            Assert.IsFalse(CompressionHelper.IsCompressedEnvelopeStrictlySmaller(100, 99, encryptedBytes));
            Assert.IsTrue(CompressionHelper.IsCompressedEnvelopeStrictlySmaller(1024, 780, encryptedBytes));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => CompressionHelper.GetBase64Length(-1));
            Assert.ThrowsException<OverflowException>(() => CompressionHelper.GetBase64Length(int.MaxValue));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => CompressionHelper.GetStoredBase64Length(-1, encryptedBytes));
            Assert.ThrowsException<InvalidOperationException>(() => CompressionHelper.GetStoredBase64Length(1, _ => -1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => CompressionHelper.IsCompressedEnvelopeStrictlySmaller(-1, 1, encryptedBytes));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => CompressionHelper.IsCompressedEnvelopeStrictlySmaller(1, -1, encryptedBytes));
        }

        [TestMethod]
        public void DecompressionBudget_ChargesAtomicallyAndValidatesLimits()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new DecompressionBudget(-1));

            DecompressionBudget budget = new (10);
            Assert.IsTrue(budget.TryCharge(6));
            Assert.AreEqual(4, budget.RemainingBytes);
            Assert.IsFalse(budget.TryCharge(5));
            Assert.IsTrue(budget.TryCharge(4));
            Assert.AreEqual(0, budget.RemainingBytes);
            Assert.IsTrue(budget.TryCharge(0));
            Assert.IsFalse(budget.TryCharge(1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => budget.TryCharge(-1));

            DecompressionBudget overflow = new (long.MaxValue);
            Assert.IsTrue(overflow.TryCharge(long.MaxValue - 1));
            Assert.IsFalse(overflow.TryCharge(2));
        }

        [TestMethod]
        public void DecompressionBudget_IsConcurrencySafe()
        {
            DecompressionBudget budget = new (1000);

            Parallel.ForEach(Enumerable.Range(0, 10000), _ => budget.TryCharge(1));

            Assert.AreEqual(0, budget.RemainingBytes);
            Assert.IsFalse(budget.TryCharge(1));
        }

        [TestMethod]
        public void CodecImplementationTypes_AreAvailableOnlyOnNet8()
        {
            Type deflate = typeof(CompressionCodecId).Assembly.GetType(
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.DeflateCompressorAdapter");
            Type brotli = typeof(CompressionCodecId).Assembly.GetType(
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.BrotliCompressorAdapter");

            Assert.IsNotNull(deflate);
            Assert.IsNotNull(brotli);
        }

        [DataTestMethod]
        [DataRow((byte)CompressionLevelSetting.Fastest, 1)]
        [DataRow((byte)CompressionLevelSetting.Balanced, 5)]
        [DataRow((byte)CompressionLevelSetting.SmallestSize, 9)]
        public void BrotliCompressorAdapter_MapsInternalLevels(byte levelValue, int expectedQuality)
        {
            Assert.AreEqual(
                expectedQuality,
                BrotliCompressorAdapter.ToQuality((CompressionLevelSetting)levelValue));
        }

        [TestMethod]
        public void BrotliCompressorAdapter_RejectsUnsupportedLevel()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                BrotliCompressorAdapter.ToQuality((CompressionLevelSetting)255));
        }

        [TestMethod]
        public void CodecAdapters_RoundTripAndEnforceDestinationSize()
        {
            byte[] source = Encoding.UTF8.GetBytes(new string('x', 8192));

            AssertRoundTrip(new DeflateCompressorAdapter(), source);
            AssertRoundTrip(new BrotliCompressorAdapter(), source);

            AssertRejectsInvalidCompressedInput(new DeflateCompressorAdapter(), source);
            AssertRejectsInvalidCompressedInput(new BrotliCompressorAdapter(), source);

            using NonArrayMemoryOwner owner = new (new byte[] { 1, 2, 3 });
            Assert.ThrowsException<InvalidOperationException>(() =>
                new DeflateCompressorAdapter().Decompress(owner.Memory, new byte[1]));
        }

        [TestMethod]
        public void BufferWriterCopyingStream_CountsOnlySuccessfulWrites()
        {
            ArrayBufferWriter<byte> destination = new ();
            BufferWriterCopyingStream stream = new (destination);
            byte[] first = { 1, 2, 3, 4 };

            stream.Write(first, 1, 2);
            stream.Write(new byte[] { 4, 5, 6 }.AsSpan());

            Assert.AreEqual(5, stream.BytesWritten);
            CollectionAssert.AreEqual(new byte[] { 2, 3, 4, 5, 6 }, destination.WrittenSpan.ToArray());

            BufferWriterCopyingStream failingStream = new (new ThrowingAdvanceBufferWriter());
            Assert.ThrowsException<InvalidOperationException>(() =>
                failingStream.Write(new byte[] { 7, 8 }, 0, 2));
            Assert.AreEqual(0, failingStream.BytesWritten);
        }

        private static void AssertRoundTrip(ICompressionCodecAdapter adapter, byte[] source)
        {
            byte[] compressed = Compress(adapter, source);

            byte[] decompressed = new byte[source.Length];
            int written = adapter.Decompress(compressed, decompressed);
            Assert.AreEqual(source.Length, written);
            CollectionAssert.AreEqual(source, decompressed);
        }

        private static void AssertRejectsInvalidCompressedInput(ICompressionCodecAdapter adapter, byte[] source)
        {
            byte[] compressed = Compress(adapter, source);

            Assert.ThrowsException<InvalidDataException>(() => adapter.Decompress(new byte[] { 1, 2, 3 }, new byte[source.Length]));
            Assert.ThrowsException<InvalidDataException>(() => adapter.Decompress(compressed.AsMemory(0, compressed.Length / 2), new byte[source.Length]));
            Assert.ThrowsException<InvalidDataException>(() => adapter.Decompress(compressed, new byte[source.Length + 1]));
            Assert.ThrowsException<InvalidDataException>(() => adapter.Decompress(compressed, new byte[source.Length - 1]));
        }

        private static byte[] Compress(ICompressionCodecAdapter adapter, byte[] source)
        {
            ArrayBufferWriter<byte> compressed = new ();
            int written = adapter.Compress(source, CompressionLevelSetting.Balanced, compressed);
            Assert.AreEqual(compressed.WrittenCount, written);
            Assert.IsTrue(compressed.WrittenCount < source.Length);
            return compressed.WrittenSpan.ToArray();
        }

        private sealed class NonArrayMemoryOwner : MemoryManager<byte>
        {
            private readonly byte[] buffer;

            public NonArrayMemoryOwner(byte[] buffer)
            {
                this.buffer = buffer;
            }

            public override Span<byte> GetSpan()
            {
                return this.buffer;
            }

            public override MemoryHandle Pin(int elementIndex = 0)
            {
                throw new NotSupportedException();
            }

            public override void Unpin()
            {
            }

            protected override void Dispose(bool disposing)
            {
            }
        }

        private sealed class ThrowingAdvanceBufferWriter : IBufferWriter<byte>
        {
            private readonly byte[] buffer = new byte[16];

            public void Advance(int count)
            {
                throw new InvalidOperationException("Advance failed.");
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                return this.buffer;
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                return this.buffer;
            }
        }
    }
}
#endif
