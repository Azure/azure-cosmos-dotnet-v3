//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests.TestHelpers
{
    using System;
    using System.IO;
    using System.Text;

    public static class StreamTestHelpers
    {
        public static MemoryStream ToStream(string json)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
        }

        public static string ReadToEnd(Stream s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            if (s.CanSeek)
            {
                s.Position = 0;
                using var sr = new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                return sr.ReadToEnd();
            }

            using var buffer = new MemoryStream();
            s.CopyTo(buffer);
            buffer.Position = 0;
            using var sr2 = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: false);
            return sr2.ReadToEnd();
        }

        // Test-only wrapper to verify disposal explicitly
        public sealed class TrackingStream : Stream
        {
            private readonly Stream inner;

            public TrackingStream(Stream inner)
            {
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public bool Disposed { get; private set; }

            public override bool CanRead => this.inner.CanRead;
            public override bool CanSeek => this.inner.CanSeek;
            public override bool CanWrite => this.inner.CanWrite;
            public override long Length => this.inner.Length;
            public override long Position { get => this.inner.Position; set => this.inner.Position = value; }

            public override void Flush() => this.inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => this.inner.Seek(offset, origin);
            public override void SetLength(long value) => this.inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => this.inner.Write(buffer, offset, count);

            protected override void Dispose(bool disposing)
            {
                if (disposing && !this.Disposed)
                {
                    this.Disposed = true;
                    this.inner.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
