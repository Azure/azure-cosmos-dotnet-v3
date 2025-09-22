// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

﻿#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Generic pooled growable buffer writer. Internal usage only.
    /// Focuses on sequential append scenarios (like List&lt;T&gt; used as a builder).
    /// </summary>
    [Flags]
    internal enum PooledBufferWriterOptions
    {
        None = 0,
        ClearOnDispose = 1,
        ClearOnExpand = 2,
        ClearOnReset = 4,

        /// <summary>
        /// Convenience mask for full clearing lifecycle.
        /// </summary>
        AlwaysClear = ClearOnDispose | ClearOnExpand | ClearOnReset,
    }

    internal class PooledBufferWriter<T> : IBufferWriter<T>, IDisposable
    {
        private const int DefaultInitialCapacity = 256;
        private readonly ArrayPool<T> pool;
        private readonly PooledBufferWriterOptions options;
        private T[] buffer;
        private int count;
        private bool disposed;

        public PooledBufferWriter(
            int initialCapacity = DefaultInitialCapacity,
            ArrayPool<T> pool = null,
            PooledBufferWriterOptions options = PooledBufferWriterOptions.None)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            this.pool = pool ?? ArrayPool<T>.Shared;
            this.options = options;
            this.buffer = initialCapacity == 0 ? Array.Empty<T>() : this.pool.Rent(initialCapacity);
            this.count = 0;
        }

        public int Count
        {
            get
            {
                this.ThrowIfDisposed();
                return this.count;
            }
        }

        public ReadOnlySpan<T> WrittenSpan
        {
            get
            {
                this.ThrowIfDisposed();
                return this.buffer.AsSpan(0, this.count);
            }
        }

        public ReadOnlyMemory<T> WrittenMemory
        {
            get
            {
                this.ThrowIfDisposed();
                return this.buffer.AsMemory(0, this.count);
            }
        }

        public void Advance(int count)
        {
            this.ThrowIfDisposed();
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (this.count > this.buffer.Length - count)
            {
                throw new InvalidOperationException("Cannot advance past the end of the buffer.");
            }

            this.count += count;
    }

        public Memory<T> GetMemory(int sizeHint = 0)
        {
            this.ThrowIfDisposed();
            if (sizeHint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            }

            this.EnsureCapacityForAdditional(sizeHint);
            return this.buffer.AsMemory(this.count);
    }

        public Span<T> GetSpan(int sizeHint = 0)
        {
            this.ThrowIfDisposed();
            if (sizeHint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            }

            this.EnsureCapacityForAdditional(sizeHint);
            return this.buffer.AsSpan(this.count);
    }

        /// <summary>
        /// Ensures the underlying array can contain at least <paramref name="capacity"/> elements total.
        /// </summary>
        public void EnsureCapacity(int capacity)
        {
            this.ThrowIfDisposed();
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (capacity <= this.buffer.Length)
            {
                return;
            }

            this.Grow(capacity);
    }

        /// <summary>
        /// Resets the logical length to zero. Optionally clears content if requested by options.
        /// </summary>
        public void Clear()
        {
            this.ThrowIfDisposed();
            if ((this.options & PooledBufferWriterOptions.ClearOnReset) != 0 && this.count != 0 && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                Array.Clear(this.buffer, 0, this.count);
            }

            this.count = 0;
    }

        public T[] ToArray()
        {
            this.ThrowIfDisposed();
            if (this.count == 0)
            {
                return Array.Empty<T>();
            }

            T[] copy = new T[this.count];
            Array.Copy(this.buffer, 0, copy, 0, this.count);
            return copy;
    }

        /// <summary>
        /// Shifts the first <paramref name="bytes"/> elements out of the written region, keeping any remaining bytes.
        /// Used by streaming parsers to remove consumed prefix without allocating.
        /// </summary>
        /// <param name="bytes">Number of leading elements to consume.</param>
        internal void ConsumePrefix(int bytes)
        {
            this.ThrowIfDisposed();
            if (bytes < 0 || bytes > this.count)
            {
                throw new ArgumentOutOfRangeException(nameof(bytes));
            }

            if (bytes == 0)
            {
                return; // Nothing consumed.
            }

            int remaining = this.count - bytes;
            if (remaining != 0)
            {
                Array.Copy(this.buffer, bytes, this.buffer, 0, remaining);
            }

            this.count = remaining;
    }

        protected void EnsureCapacityForAdditional(int sizeHint)
        {
            // sizeHint of 0 means at least one element (like ArrayBufferWriter convention) but for generic T we treat 0 as minimal.
            int required = this.count + (sizeHint <= 0 ? 1 : sizeHint);
            if (required <= this.buffer.Length)
            {
                return;
            }

            this.Grow(required);
    }

        private void Grow(int requiredCapacity)
        {
            int newCapacity = this.buffer.Length == 0 ? Math.Max(requiredCapacity, DefaultInitialCapacity) : this.buffer.Length * 2;
            if (newCapacity < requiredCapacity)
            {
                newCapacity = requiredCapacity;
            }

            if ((uint)newCapacity > Array.MaxLength)
            {
                newCapacity = Array.MaxLength;
            }

            T[] old = this.buffer;
            T[] newBuffer = this.pool.Rent(newCapacity);
            if (this.count != 0)
            {
                Array.Copy(old, 0, newBuffer, 0, this.count);
            }

            if (old.Length != 0)
            {
                bool clear = (this.options & PooledBufferWriterOptions.ClearOnExpand) != 0 && RuntimeHelpers.IsReferenceOrContainsReferences<T>();
                this.pool.Return(old, clearArray: clear);
            }

            this.buffer = newBuffer;
    }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
    }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            if (this.buffer.Length != 0)
            {
                bool clear = (this.options & PooledBufferWriterOptions.ClearOnDispose) != 0 && RuntimeHelpers.IsReferenceOrContainsReferences<T>();
                this.pool.Return(this.buffer, clearArray: clear);
            }

            this.buffer = Array.Empty<T>();
            this.count = 0;
    }

        internal T[] GetInternalArray() => this.buffer;

        /// <summary>
        /// Gets the remaining free capacity in the current underlying buffer.
        /// </summary>
        internal int FreeCapacity => this.buffer.Length - this.count;
    }
}
#endif
