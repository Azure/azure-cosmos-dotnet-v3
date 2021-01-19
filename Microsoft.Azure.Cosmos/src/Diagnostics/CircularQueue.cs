//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    ///  simple circular queue that preallocates the underlying buffer
    /// </summary>
    internal sealed class CircularQueue<T> : IEnumerable<T>
    {
        private readonly T[] buffer;

        private int head;
        private int tail;

        /// <summary>
        /// Capacity of the queue.
        /// </summary>
        public int Capacity => this.buffer.Length;

        /// <summary>
        /// True if adding an element will cause one to be evicted.
        /// </summary>
        public bool Full => this.GetNextIndex(this.tail) == this.head;

        /// <summary>
        /// True when the queue is empty.
        /// </summary>
        public bool Empty => this.tail == this.head;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularQueue{T}"/> class.
        /// </summary>
        /// <param name="capacity"></param>
        private CircularQueue(int capacity)
        {
            this.head = 0;
            this.tail = 0;
            this.buffer = new T[capacity];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularQueue{T}"/> class.
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="queue">the newly created queue</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool TryCreate(int capacity, out CircularQueue<T> queue)
        {
            queue = null;
            if (capacity > 0)
            {
                queue = new CircularQueue<T>(capacity + 1);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a new element to the queue. Can cause an older element to be evicted.
        /// </summary>
        /// <param name="element"></param>
        public void Add(T element)
        {
            if (this.Full)
            {
                this.TryPop(out _);
            }

            this.buffer[this.tail] = element;
            this.tail = this.GetNextIndex(this.tail);
        }

        /// <summary>
        /// Adds a subrange of the argument to the queue depending on capacity.
        /// </summary>
        /// <param name="elements"></param>
        public void AddRange(IEnumerable<T> elements)
        {
            foreach (T element in elements)
            {
                this.Add(element);
            }
        }

        private int GetNextIndex(int index)
        {
            return (index + 1) % this.Capacity;
        }

        private bool TryPop(out T element)
        {
            element = default;
            if (!this.Empty)
            {
                element = this.buffer[this.head];
                this.head = this.GetNextIndex(this.head);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            if (!this.Empty)
            {
                for (int i = this.head; i != this.tail; i = this.GetNextIndex(i))
                {
                    yield return this.buffer[i];
                }
            }
        }
    }
}