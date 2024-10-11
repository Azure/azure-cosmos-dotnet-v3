namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
#if NET8_0_OR_GREATER
    using System;
    using System.Collections.Generic;
    using System.IO.Compression;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BrotliCompressionTests
    {
        [TestMethod]
        [DataRow(CompressionLevel.NoCompression, 0)]
        [DataRow(CompressionLevel.Fastest, 1)]
        [DataRow(CompressionLevel.Optimal, 4)]
        [DataRow(CompressionLevel.SmallestSize, 11)]
        public void GetQuality_WorksForKnownLevels(CompressionLevel compressionLevel, int expectedQuality)
        {
            Assert.AreEqual(BrotliCompressor.GetQualityFromCompressionLevel(compressionLevel), expectedQuality);
        }

        [TestMethod]
        public void GetQuality_ThrowsForUnknownLevels()
        {
            Assert.ThrowsException<ArgumentException>(() => BrotliCompressor.GetQualityFromCompressionLevel((CompressionLevel)(-999)));
        }

        [TestMethod]
        [DataRow(CompressionLevel.NoCompression, 256)]
        [DataRow(CompressionLevel.Fastest, 256)]
        [DataRow(CompressionLevel.Optimal, 256)]
        [DataRow(CompressionLevel.SmallestSize, 256)]
        [DataRow(CompressionLevel.NoCompression, 1024)]
        [DataRow(CompressionLevel.Fastest, 1024)]
        [DataRow(CompressionLevel.Optimal, 1024)]
        [DataRow(CompressionLevel.SmallestSize, 1024)]
        [DataRow(CompressionLevel.NoCompression, 4096)]
        [DataRow(CompressionLevel.Fastest, 4096)]
        [DataRow(CompressionLevel.Optimal, 4096)]
        [DataRow(CompressionLevel.SmallestSize, 4096)]
        public void CompressAndDecompress_HasSameResult(CompressionLevel compressionLevel, int payloadSize)
        {
            BrotliCompressor compressor = new (compressionLevel);
            Dictionary<string, int> properties = new ();
            string path = "somePath";

            byte[] bytes = new byte[payloadSize];
            bytes.AsSpan().Fill(127);

            byte[] compressedBytes = new byte[BrotliCompressor.GetMaxCompressedSize(payloadSize)];
            int compressedBytesSize = compressor.Compress(properties, path, bytes, bytes.Length, compressedBytes);

            Assert.AreNotSame(bytes, compressedBytes);
            Assert.IsTrue(compressedBytesSize > 0);
            Assert.IsTrue(compressedBytesSize < bytes.Length);

            Console.WriteLine($"Original: {bytes.Length} Compressed: {compressedBytesSize}");
            
            Assert.IsTrue(properties.ContainsKey(path));

            int recordedSize = properties["somePath"];
            Assert.AreEqual(bytes.Length, recordedSize);

            byte[] decompressedBytes = new byte[recordedSize];
            int decompressedBytesSize = compressor.Decompress(compressedBytes, compressedBytesSize, decompressedBytes);

            Assert.AreEqual(decompressedBytesSize, bytes.Length);
            Assert.IsTrue(bytes.SequenceEqual(decompressedBytes));
        }
    }
#endif
}
