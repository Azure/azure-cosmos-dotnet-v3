//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;

    internal class ArrayPoolManager : IDisposable
    {
        private List<byte[]> rentedBuffers = new List<byte[]>();
        private bool disposedValue;

        public byte[] Rent(int minimumLength)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(minimumLength);
            this.rentedBuffers.Add(buffer);
            return buffer;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    foreach (byte[] buffer in this.rentedBuffers)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                this.rentedBuffers = null;
                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
