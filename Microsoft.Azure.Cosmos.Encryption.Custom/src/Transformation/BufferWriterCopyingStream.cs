// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;
    using System.IO;

    internal sealed class BufferWriterCopyingStream : Stream
    {
        private readonly IBufferWriter<byte> writer;

        public BufferWriterCopyingStream(IBufferWriter<byte> writer)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public int BytesWritten { get; private set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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
            buffer.AsSpan(offset, count).CopyTo(this.writer.GetSpan(count));
            this.writer.Advance(count);
            this.BytesWritten += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            buffer.CopyTo(this.writer.GetSpan(buffer.Length));
            this.writer.Advance(buffer.Length);
            this.BytesWritten += buffer.Length;
        }
    }
}
#endif
