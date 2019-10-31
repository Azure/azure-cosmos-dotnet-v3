// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;

    internal sealed class CosmosStreamContent : RequestContent
    {
        private const int CopyToBufferSize = 81920;
        private readonly long Origin;

        private Stream stream;

        public CosmosStreamContent(Stream stream)
        {
            if (!stream.CanSeek) throw new ArgumentException("stream must be seekable", nameof(stream));

            this.Origin = stream.Position;
            this.stream = stream;
        }

        public override void WriteTo(Stream stream, CancellationToken cancellationToken)
        {
            this.stream.Seek(this.Origin, SeekOrigin.Begin);

            // this is not using CopyTo so that we can honor cancellations.
            byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyToBufferSize);
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = this.stream.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        break;
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    stream.Write(buffer, 0, read);
                }
            }
            finally
            {
                stream.Flush();
                ArrayPool<byte>.Shared.Return(buffer, true);
            }
        }

        public override bool TryComputeLength(out long length)
        {
            if (this.stream.CanSeek)
            {
                length = this.stream.Length - this.Origin;
                return true;
            }
            length = 0;
            return false;
        }

        public override async Task WriteToAsync(Stream stream, CancellationToken cancellation)
        {
            this.stream.Seek(this.Origin, SeekOrigin.Begin);
            await this.stream.CopyToAsync(stream, CopyToBufferSize, cancellation).ConfigureAwait(false);
        }

        public Stream Detach()
        {
            Stream response = this.stream;
            this.stream = null;

            return response;
        }

        public override void Dispose()
        {
            if (this.stream != null)
            {
                this.stream.Dispose();
            }
        }
    }
}
