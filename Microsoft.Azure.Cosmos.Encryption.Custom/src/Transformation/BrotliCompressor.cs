// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
#if NET8_0_OR_GREATER

    using System;
    using System.Diagnostics;
    using System.IO.Compression;

    internal class BrotliCompressor
    {
        internal static int GetQualityFromCompressionLevel(CompressionLevel compressionLevel)
        {
            return compressionLevel switch
            {
                CompressionLevel.NoCompression => 0,
                CompressionLevel.Fastest => 1,
                CompressionLevel.Optimal => 4,
                CompressionLevel.SmallestSize => 11,
                _ => throw new ArgumentException("Unsupported compression level", nameof(compressionLevel))
            };
        }

        public virtual (byte[], int) Compress(EncryptionProperties properties, string path, byte[] bytes, int length, ArrayPoolManager arrayPoolManager, int compressionLevel)
        {
            byte[] compressedBytes = arrayPoolManager.Rent(BrotliEncoder.GetMaxCompressedLength(length));

            if (!BrotliEncoder.TryCompress(bytes.AsSpan(0, length), compressedBytes, out int bytesWritten, compressionLevel, 22))
            {
                throw new InvalidOperationException();
            }

            properties.CompressedEncryptedPaths[path] = length;

            return (compressedBytes, bytesWritten);
        }

        public virtual int Decompress(byte[] inputBytes, int length, byte[] outputBytes)
        {
            if (!BrotliDecoder.TryDecompress(inputBytes.AsSpan(0, length), outputBytes, out int bytesWritten))
            {
                throw new InvalidOperationException();
            }

            return bytesWritten;
        }
    }
#endif
}
