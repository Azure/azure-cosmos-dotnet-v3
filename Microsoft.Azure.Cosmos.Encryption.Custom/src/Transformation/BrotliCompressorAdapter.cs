// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.IO.Compression;

    internal sealed class BrotliCompressorAdapter : ICompressionCodecAdapter
    {
        public int Compress(
            ReadOnlySpan<byte> source,
            CompressionLevelSetting level,
            IBufferWriter<byte> destination)
        {
            Span<byte> target = destination.GetSpan(BrotliEncoder.GetMaxCompressedLength(source.Length));
            if (!BrotliEncoder.TryCompress(
                source,
                target,
                out int written,
                ToQuality(level),
                window: 22))
            {
                throw new InvalidDataException("Brotli compression failed.");
            }

            destination.Advance(written);
            return written;
        }

        public int Decompress(
            ReadOnlyMemory<byte> source,
            Span<byte> destination)
        {
            using BrotliDecoder decoder = default;
            OperationStatus status = decoder.Decompress(
                source.Span,
                destination,
                out int bytesConsumed,
                out int bytesWritten);
            if (status != OperationStatus.Done ||
                bytesConsumed != source.Length ||
                bytesWritten != destination.Length)
            {
                throw new InvalidDataException("Brotli decompression failed.");
            }

            return bytesWritten;
        }

        internal static int ToQuality(CompressionLevelSetting level)
        {
            return level switch
            {
                CompressionLevelSetting.Fastest => 1,
                CompressionLevelSetting.Balanced => 5,
                CompressionLevelSetting.SmallestSize => 9,
                _ => throw new ArgumentException("Unsupported compression level.", nameof(level)),
            };
        }
    }
}
#endif
