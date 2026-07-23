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
        // Covers the typical decrypt rent count (~2 per encrypted property + structural)
        // so the List<T[]> does not grow through 4/8/16/.../256 on every op.
        private const int DefaultRentCapacity = 16;

        private List<T[]> rentedBuffers;
        private T[] scratch;
        private bool disposedValue;

        public ArrayPoolManager()
            : this(DefaultRentCapacity)
        {
        }

        public ArrayPoolManager(int initialRentCapacity)
        {
            this.rentedBuffers = new List<T[]>(initialRentCapacity <= 0 ? DefaultRentCapacity : initialRentCapacity);
        }

        public T[] Rent(int minimumLength)
        {
            T[] buffer = ArrayPool<T>.Shared.Rent(minimumLength);
            this.rentedBuffers.Add(buffer);
            return buffer;
        }

        /// <summary>
        /// Rents a single reusable scratch buffer for transient staging where the copied bytes are
        /// fully consumed before the next call (e.g. copy-then-write). The same buffer is returned
        /// across calls, growing only when a larger minimum length is requested, so a document with
        /// many small transient copies (e.g. escaped pass-through strings/property names) does not
        /// churn the shared pool with one rental per copy. The scratch buffer is returned (and
        /// cleared) together with the rest on <see cref="Dispose()"/>.
        /// </summary>
        public T[] RentScratch(int minimumLength)
        {
            if (this.scratch == null || this.scratch.Length < minimumLength)
            {
                this.scratch = this.Rent(minimumLength);
            }

            return this.scratch;
        }

        internal int RentedBufferCount => this.rentedBuffers?.Count ?? 0;

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
                    this.scratch = null;
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
        public ArrayPoolManager()
        {
        }

        public ArrayPoolManager(int initialRentCapacity)
            : base(initialRentCapacity)
        {
        }
    }
}
