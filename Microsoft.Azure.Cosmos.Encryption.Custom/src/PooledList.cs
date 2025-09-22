// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
#pragma warning disable SA1514, SA1516, SA1214, SA1513, SA1515, SA1401
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Lightweight List-like abstraction backed by <see cref="PooledBufferWriter{T}"/>.
    /// Supports only append semantics (<see cref="Add"/>, <see cref="AddRange(ReadOnlySpan{T})"/>)
    /// and indexed get/set. Internal use only; intentionally minimal API surface.
    /// </summary>
    /// <remarks>
    /// Chosen over exposing the writer directly where random access (read or overwrite) is useful
    /// but structural mutations other than append are not required.
    /// </remarks>
    internal sealed class PooledList<T> : IEnumerable<T>, IDisposable
    {
        private readonly PooledBufferWriter<T> writer;
        private bool disposed;

        public PooledList(int initialCapacity = 0, PooledBufferWriterOptions options = PooledBufferWriterOptions.None)
        {
            this.writer = new PooledBufferWriter<T>(initialCapacity, options: options);
        }

        public int Count
        {
            get
            {
                this.ThrowIfDisposed();
                return this.writer.Count;
            }
        }

        public T this[int index]
        {
            get
            {
                this.ThrowIfDisposed();
                if ((uint)index >= (uint)this.writer.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                return this.writer.GetInternalArray()[index];
            }
            set
            {
                this.ThrowIfDisposed();
                if ((uint)index >= (uint)this.writer.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                this.writer.GetInternalArray()[index] = value;
            }
        }

        public void Add(T item)
        {
            this.ThrowIfDisposed();
            Span<T> span = this.writer.GetSpan(1);
            span[0] = item;
            this.writer.Advance(1);
        }

        public void AddRange(ReadOnlySpan<T> items)
        {
            this.ThrowIfDisposed();
            if (items.Length == 0)
            {
                return;
            }
            this.writer.EnsureCapacity(this.writer.Count + items.Length);
            items.CopyTo(this.writer.GetInternalArray().AsSpan(this.writer.Count));
            this.writer.Advance(items.Length);
        }

        public void Clear() => this.writer.Clear();

        public T[] ToArray() => this.writer.ToArray();

        public Enumerator GetEnumerator()
        {
            this.ThrowIfDisposed();
            return new Enumerator(this.writer.GetInternalArray(), this.writer.Count);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => this.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }
            this.disposed = true;
            this.writer.Dispose();
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
        }

        internal struct Enumerator : IEnumerator<T>
        {
            private readonly T[] buffer;
            private readonly int count;
            private int index;

            internal Enumerator(T[] buffer, int count)
            {
                this.buffer = buffer;
                this.count = count;
                this.index = -1;
            }

            public T Current => this.buffer[this.index];

            object IEnumerator.Current => this.Current;

            public bool MoveNext()
            {
                int next = this.index + 1;
                if (next < this.count)
                {
                    this.index = next;
                    return true;
                }
                return false;
            }

            public void Reset() => this.index = -1;

            public void Dispose()
            {
            }
        }
    }
}
#pragma warning restore SA1514, SA1516, SA1214, SA1513, SA1515, SA1401
#endif
