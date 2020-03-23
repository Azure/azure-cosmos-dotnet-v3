//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class MediaStream : Stream
    {
        private readonly HttpResponseMessage responseMessage;
        private readonly Stream contentStream;
        private bool isDisposed;

        public MediaStream(HttpResponseMessage responseMessage, Stream contentStream)
        {
            this.responseMessage = responseMessage;
            this.contentStream = contentStream;
            this.isDisposed = false;
        }

        public override bool CanRead
        {
            get
            {
                return this.contentStream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return this.contentStream.CanSeek;
            }
        }

        public override bool CanTimeout
        {
            get
            {
                return this.contentStream.CanTimeout;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return this.contentStream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return this.contentStream.Length;
            }
        }

        public override int ReadTimeout
        {
            get
            {
                return this.contentStream.ReadTimeout;
            }
            set
            {
                this.contentStream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                return this.contentStream.WriteTimeout;
            }
            set
            {
                this.contentStream.WriteTimeout = value;
            }
        }

        public override long Position
        {
            get
            {
                return this.contentStream.Position;
            }
            set
            {
                this.contentStream.Position = value;
            }
        }

#if !NETSTANDARD16
        public override void Close()
        {
            this.contentStream.Close();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this.contentStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return this.contentStream.EndRead(asyncResult);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this.contentStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            this.contentStream.EndWrite(asyncResult);
        }

        public override object InitializeLifetimeService()
        {
            return this.contentStream.InitializeLifetimeService();
        }
#endif

#if !(NETSTANDARD16 || NETSTANDARD20)
        public override System.Runtime.Remoting.ObjRef CreateObjRef(Type requestedType)
        {
            return this.contentStream.CreateObjRef(requestedType);
        }
#endif

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.contentStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.contentStream.Write(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.contentStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.contentStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return this.contentStream.CopyToAsync(destination, bufferSize, cancellationToken);
        }
        public override void Flush()
        {
            this.contentStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return this.contentStream.FlushAsync(cancellationToken);
        }

        public override int ReadByte()
        {
            return this.contentStream.ReadByte();
        }

        public override void WriteByte(byte value)
        {
            this.contentStream.WriteByte(value);
        }

        public override void SetLength(long value)
        {
            this.contentStream.SetLength(value);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.contentStream.Seek(offset, origin);
        }
        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed && disposing)
            {
                // This will dispose message as well as content stream. 
                // No need to dispose contentStream explicitly
                this.responseMessage.Dispose();
                this.isDisposed = true;
            }

            base.Dispose(disposing);
        }
    }
}