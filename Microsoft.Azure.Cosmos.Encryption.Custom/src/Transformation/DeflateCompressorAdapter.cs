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
    using System.Runtime.InteropServices;

    internal sealed class DeflateCompressorAdapter : ICompressionCodecAdapter
    {
        public int Compress(
            ReadOnlySpan<byte> source,
            CompressionLevelSetting level,
            IBufferWriter<byte> destination)
        {
            BufferWriterCopyingStream sink = new (destination);
            using (DeflateStream stream = new (
                sink,
                ToCompressionLevel(level),
                leaveOpen: true))
            {
                stream.Write(source);
            }

            return sink.BytesWritten;
        }

        public int Decompress(
            ReadOnlyMemory<byte> source,
            Span<byte> destination)
        {
            if (!MemoryMarshal.TryGetArray(source, out ArraySegment<byte> sourceSegment))
            {
                throw new InvalidOperationException("Deflate input must be backed by an array.");
            }

            // Exact decompressed length and over-expansion are checked below. Exact compressed input
            // consumption cannot be efficiently observed because .NET 8 DeflateStream buffers and reads ahead.
            int total = 0;
            using (MemoryStream input = new (
                sourceSegment.Array,
                sourceSegment.Offset,
                sourceSegment.Count,
                writable: false))
            using (DeflateStream stream = new (input, CompressionMode.Decompress, leaveOpen: false))
            {
                while (total < destination.Length)
                {
                    int read = stream.Read(destination.Slice(total));
                    if (read == 0)
                    {
                        break;
                    }

                    total += read;
                }

                Span<byte> overExpansionProbe = stackalloc byte[1];
                if (stream.Read(overExpansionProbe) != 0)
                {
                    throw new InvalidDataException("Deflate decompressed more bytes than declared.");
                }
            }

            if (total != destination.Length)
            {
                throw new InvalidDataException("Deflate decompressed length did not match the declared length.");
            }

            return total;
        }

        private static CompressionLevel ToCompressionLevel(CompressionLevelSetting level)
        {
            return level switch
            {
                CompressionLevelSetting.Fastest => CompressionLevel.Fastest,
                CompressionLevelSetting.Balanced => CompressionLevel.Optimal,
                CompressionLevelSetting.SmallestSize => CompressionLevel.SmallestSize,
                _ => throw new ArgumentException("Unsupported compression level.", nameof(level)),
            };
        }
    }
}
#endif
