//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class CloneableStream : Stream, ICloneable
    {
        private readonly MemoryStream internalStream;
        private readonly bool allowUnsafeDataAccess;

        public CloneableStream Clone()
        {
            return new CloneableStream(this.CloneStream(), this.allowUnsafeDataAccess);
        }

        object ICloneable.Clone()
        {
            return new CloneableStream(this.CloneStream(), this.allowUnsafeDataAccess);
        }

        private MemoryStream CloneStream()
        {
            if (this.internalStream is ICloneable cloneableStream)
            {
                MemoryStream memoryStream = (MemoryStream)cloneableStream.Clone();
                memoryStream.Position = 0;
                return memoryStream;
            }

            if (!this.allowUnsafeDataAccess)
            {
                throw new NotSupportedException($"Cloning the stream is not a supported method when {nameof(this.allowUnsafeDataAccess)} is set to false and stream does not implement ICloneable");
            }
            
            return new MemoryStream(buffer: this.internalStream.GetBuffer(), index: 0, count: (int)this.internalStream.Length, writable: false, publiclyVisible: true);
        }

        public CloneableStream(MemoryStream internalStream, bool allowUnsafeDataAccess = true)
        {
            this.internalStream = internalStream;
            this.allowUnsafeDataAccess = allowUnsafeDataAccess;
        }

        public override bool CanRead
        {
            get
            {
                return this.internalStream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return this.internalStream.CanSeek;
            }
        }

        public override bool CanTimeout
        {
            get
            {
                return this.internalStream.CanTimeout;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return this.internalStream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return this.internalStream.Length;
            }
        }

        public override long Position
        {
            get
            {
                return this.internalStream.Position;
            }
            set
            {
                this.internalStream.Position = value;
            }
        }

        public override int ReadTimeout
        {
            get
            {
                return this.internalStream.ReadTimeout;
            }
            set
            {
                this.internalStream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                return this.internalStream.WriteTimeout;
            }
            set
            {
                this.internalStream.WriteTimeout = value;
            }
        }

        public ArraySegment<byte> GetBuffer()
        {
            if (!this.allowUnsafeDataAccess)
            {
                throw new NotSupportedException($"{nameof(GetBuffer)} is not a supported method when {nameof(this.allowUnsafeDataAccess)} is set to false");
            }

            return new ArraySegment<byte>(this.internalStream.GetBuffer(), 0, (int)this.internalStream.Length);
        }

#if !NETSTANDARD16
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
        {
            return this.internalStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
        {
            return this.internalStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void Close()
        {
            this.internalStream.Close();
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return this.internalStream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            this.internalStream.EndWrite(asyncResult);
        }
#endif

        public override void Flush()
        {
            this.internalStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return this.internalStream.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.internalStream.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.internalStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int ReadByte()
        {
            return this.internalStream.ReadByte();
        }

        public override long Seek(long offset, SeekOrigin loc)
        {
            return this.internalStream.Seek(offset, loc);
        }

        public override void SetLength(long value)
        {
            this.internalStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.internalStream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.internalStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void WriteByte(byte value)
        {
            this.internalStream.WriteByte(value);
        }

        protected override void Dispose(bool disposing)
        {
            this.internalStream.Dispose();
        }

        public void WriteTo(Stream target)
        {
            this.internalStream.WriteTo(target);
        }

        public void CopyBufferTo(byte[] buffer, int offset)
        {
            if (!this.allowUnsafeDataAccess)
            {
                this.internalStream.Write(buffer, offset, (int)this.internalStream.Length);
            }
            else
            {
                ArraySegment<byte> internalStreamBuffer = this.GetBuffer();
                Array.Copy(
                    internalStreamBuffer.Array,
                    internalStreamBuffer.Offset,
                    buffer,
                    offset,
                    internalStreamBuffer.Count);
            }
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return this.internalStream.CopyToAsync(destination, bufferSize, cancellationToken);
        }
    }
}
