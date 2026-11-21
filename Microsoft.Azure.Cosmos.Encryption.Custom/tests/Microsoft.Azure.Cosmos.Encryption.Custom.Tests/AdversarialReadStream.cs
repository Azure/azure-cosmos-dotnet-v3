//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class AdversarialReadStream : Stream
    {
        private readonly byte[] asynchronousContent;
        private readonly byte[] synchronousContent;
        private readonly Exception resetFailure;
        private int asynchronousPosition;
        private int synchronousPosition;
        private bool synchronousReadStarted;

        public AdversarialReadStream(
            byte[] asynchronousContent,
            byte[] synchronousContent,
            Exception resetFailure)
        {
            this.asynchronousContent = asynchronousContent;
            this.synchronousContent = synchronousContent;
            this.resetFailure = resetFailure;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => this.asynchronousContent.Length;

        public override long Position
        {
            get => Math.Max(this.asynchronousPosition, this.synchronousPosition);
            set
            {
                if (value != 0)
                {
                    throw new NotSupportedException();
                }

                if (this.synchronousReadStarted)
                {
                    throw this.resetFailure;
                }

                this.asynchronousPosition = 0;
                this.synchronousPosition = 0;
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            this.synchronousReadStarted = true;
            return ReadFrom(this.synchronousContent, ref this.synchronousPosition, buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            this.synchronousReadStarted = true;
            return ReadFrom(this.synchronousContent, ref this.synchronousPosition, buffer);
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ReadFrom(
                this.asynchronousContent,
                ref this.asynchronousPosition,
                buffer.AsSpan(offset, count)));
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ReadFrom(
                this.asynchronousContent,
                ref this.asynchronousPosition,
                buffer.Span));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private static int ReadFrom(byte[] source, ref int position, Span<byte> destination)
        {
            int count = Math.Min(source.Length - position, destination.Length);
            source.AsSpan(position, count).CopyTo(destination);
            position += count;
            return count;
        }
    }
}