//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;

#pragma warning disable SA1402 // File may only contain a single type
    internal class ArrayPoolManager<T> : IDisposable
#pragma warning restore SA1402 // File may only contain a single type
    {
        private List<T[]> rentedBuffers = new ();
        private bool disposedValue;

        public T[] Rent(int minimumLength)
        {
            T[] buffer = ArrayPool<T>.Shared.Rent(minimumLength);
            this.rentedBuffers.Add(buffer);
            return buffer;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    foreach (T[] buffer in this.rentedBuffers)
                    {
                        ArrayPool<T>.Shared.Return(buffer, clearArray: true);
                    }

                    this.rentedBuffers = null;
                }

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

    internal class ArrayPoolManager : ArrayPoolManager<byte>
    {
    }
}
