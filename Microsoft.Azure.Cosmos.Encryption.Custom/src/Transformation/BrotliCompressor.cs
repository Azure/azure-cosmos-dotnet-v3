// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
#if NET8_0_OR_GREATER

    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO.Compression;

    internal class BrotliCompressor : IDisposable
    {
        private const int DefaultWindow = 22;

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

        internal static int GetMaxCompressedSize(int inputSize)
        {
            return BrotliEncoder.GetMaxCompressedLength(inputSize);
        }

        private readonly BrotliDecoder decoder;
        private readonly BrotliEncoder encoder;
        private bool disposedValue;

        public BrotliCompressor()
        {
        }

        public BrotliCompressor(CompressionLevel compressionLevel)
        {
            this.encoder = new BrotliEncoder(BrotliCompressor.GetQualityFromCompressionLevel(compressionLevel), DefaultWindow);
        }

        public virtual int Compress(Dictionary<string, int> compressedPaths, string path, byte[] bytes, int length, byte[] outputBytes)
        {
            OperationStatus status = this.encoder.Compress(bytes.AsSpan(0, length), outputBytes, out int bytesConsumed, out int bytesWritten, true);

            ThrowIfFailure(status, length, bytesConsumed);

            compressedPaths[path] = length;

            return bytesWritten;
        }

        public virtual int Decompress(byte[] inputBytes, int length, byte[] outputBytes)
        {
            OperationStatus status = this.decoder.Decompress(inputBytes.AsSpan(0, length), outputBytes, out int bytesConsumed, out int bytesWritten);

            ThrowIfFailure(status, length, bytesConsumed);

            return bytesWritten;
        }

        private static void ThrowIfFailure(OperationStatus status, int expectedLength, int processedLength)
        {
            if (status != OperationStatus.Done)
            {
                throw new InvalidOperationException($"Brotli compressor failed : {status}");
            }

            if (expectedLength != processedLength)
            {
                throw new InvalidOperationException($"Expected to process {expectedLength} but only processed {processedLength}");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                this.encoder.Dispose();

                this.disposedValue = true;
            }
        }

        ~BrotliCompressor()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
#endif
}
