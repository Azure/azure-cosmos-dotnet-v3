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

        public void Return(T[] buffer)
        {
            if (buffer == null)
            {
                return;
            }

            int idx = this.rentedBuffers?.IndexOf(buffer) ?? -1;
            if (idx >= 0)
            {
                this.rentedBuffers.RemoveAt(idx);
                ArrayPool<T>.Shared.Return(buffer, clearArray: true);
            }
        }

#if DEBUG
        // Debug-only ownership probe to assert correct pooling contracts without impacting release perf
        public bool IsOwned(T[] buffer)
        {
            if (buffer == null || this.rentedBuffers == null)
            {
                return false;
            }

            return this.rentedBuffers.IndexOf(buffer) >= 0;
        }
#endif

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
