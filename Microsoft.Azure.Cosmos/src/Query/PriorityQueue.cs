//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Collections.Generic
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    /// <summary> 
    /// An implementation of <a href="https://en.wikipedia.org/wiki/Binary_heap">Binary Heap</a>
    /// </summary>
    internal sealed class PriorityQueue<T> : IProducerConsumerCollection<T>
    {
        private const int DefaultInitialCapacity = 17;
        private readonly bool isSynchronized;
        private readonly List<T> queue;
        private readonly IComparer<T> comparer;

        public PriorityQueue(bool isSynchronized = false)
            : this(DefaultInitialCapacity, isSynchronized)
        {
        }

        public PriorityQueue(int initialCapacity, bool isSynchronized = false)
            : this(initialCapacity, Comparer<T>.Default, isSynchronized)
        {
        }

        public PriorityQueue(IComparer<T> comparer, bool isSynchronized = false)
            : this(DefaultInitialCapacity, comparer, isSynchronized)
        {
        }

        public PriorityQueue(IEnumerable<T> enumerable, bool isSynchronized = false)
            : this(enumerable, Comparer<T>.Default, isSynchronized)
        {
        }

        public PriorityQueue(IEnumerable<T> enumerable, IComparer<T> comparer, bool isSynchronized = false)
            : this(new List<T>(enumerable), comparer, isSynchronized)
        {
            this.Heapify();
        }

        public PriorityQueue(int initialCapacity, IComparer<T> comparer, bool isSynchronized = false)
            : this(new List<T>(initialCapacity), comparer, isSynchronized)
        {
        }

        private PriorityQueue(List<T> queue, IComparer<T> comparer, bool isSynchronized)
        {
            if (queue == null)
            {
                throw new ArgumentNullException("queue");
            }

            if (comparer == null)
            {
                throw new ArgumentNullException("comparer");
            }

            this.isSynchronized = isSynchronized;
            this.queue = queue;
            this.comparer = comparer;
        }

        public int Count
        {
            get
            {
                return this.queue.Count;
            }
        }

        public IComparer<T> Comparer
        {
            get
            {
                return this.comparer;
            }
        }

        public bool IsSynchronized
        {
            get { return this.isSynchronized; }
        }

        public object SyncRoot
        {
            get { return this; }
        }

        public void CopyTo(T[] array, int index)
        {
            if (this.isSynchronized)
            {
                lock (this.SyncRoot)
                {
                    this.CopyToPrivate(array, index);
                    return;
                }
            }

            this.CopyToPrivate(array, index);
        }

        public bool TryAdd(T item)
        {
            this.Enqueue(item);
            return true;
        }

        public bool TryTake(out T item)
        {
            if (this.isSynchronized)
            {
                lock (this.SyncRoot)
                {
                    return this.TryTakePrivate(out item);
                }
            }

            return this.TryTakePrivate(out item);
        }

        public bool TryPeek(out T item)
        {
            if (this.isSynchronized)
            {
                lock (this.SyncRoot)
                {
                    return this.TryPeekPrivate(out item);
                }
            }

            return this.TryPeekPrivate(out item);
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            if (this.isSynchronized)
            {
                lock (this.SyncRoot)
                {
                    this.ClearPrivate();
                    return;
                }
            }

            this.ClearPrivate();
        }

        public bool Contains(T item)
        {
            if (this.isSynchronized)
            {
                lock (this.SyncRoot)
                {
                    return this.ContainsPrivate(item);
                }
            }

            return this.ContainsPrivate(item);
        }

        public T Dequeue()
        {
            if (this.isSynchronized)
            {
                lock (this.SyncRoot)
                {
                    return this.DequeuePrivate();
                }
            }

            return this.DequeuePrivate();
        }

        public void Enqueue(T item)
        {
            if (this.isSynchronized)
            {
                lock (this.SyncRoot)
                {
                    this.EnqueuePrivate(item);
                    return;
                }
            }

            this.EnqueuePrivate(item);
        }

        public void EnqueueRange(IEnumerable<T> items)
        {
            if (this.isSynchronized)
            {
                lock (this.SyncRoot)
                {
                    this.EnqueueRangePrivate(items);
                    return;
                }
            }

            this.EnqueueRangePrivate(items);
        }

        public T Peek()
        {
            if (this.isSynchronized)
            {
                lock (this.SyncRoot)
                {
                    return this.PeekPrivate();
                }
            }

            return this.PeekPrivate();
        }

        public T[] ToArray()
        {
            if (this.isSynchronized)
            {
                lock (this.SyncRoot)
                {
                    return this.ToArrayPrivate();
                }
            }

            return this.ToArrayPrivate();
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (this.isSynchronized)
            {
                lock (this.SyncRoot)
                {
                    return this.GetEnumeratorPrivate();
                }
            }

            return this.GetEnumeratorPrivate();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private void CopyToPrivate(T[] array, int index)
        {
            this.queue.CopyTo(array, index);
        }

        private bool TryTakePrivate(out T item)
        {
            if (this.queue.Count <= 0)
            {
                item = default(T);
                return false;
            }

            item = this.DequeuePrivate();
            return true;
        }

        private bool TryPeekPrivate(out T item)
        {
            if (this.queue.Count <= 0)
            {
                item = default(T);
                return false;
            }

            item = this.PeekPrivate();
            return true;
        }

        private void ClearPrivate()
        {
            this.queue.Clear();
        }

        private bool ContainsPrivate(T item)
        {
            return this.queue.Contains(item);
        }

        private T DequeuePrivate()
        {
            if (this.queue.Count <= 0)
            {
                throw new InvalidOperationException("No more elements");
            }

            T result = this.queue[0];
            this.queue[0] = this.queue[this.queue.Count - 1];
            this.queue.RemoveAt(this.queue.Count - 1);
            this.DownHeap(0);
            return result;
        }

        private void EnqueuePrivate(T item)
        {
            this.queue.Add(item);
            this.UpHeap(this.queue.Count - 1);
        }

        private void EnqueueRangePrivate(IEnumerable<T> items)
        {
            this.queue.AddRange(items);
            this.Heapify();
        }

        private T PeekPrivate()
        {
            if (this.queue.Count <= 0)
            {
                throw new InvalidOperationException("No more elements");
            }

            return this.queue[0];
        }

        private T[] ToArrayPrivate()
        {
            return this.queue.ToArray();
        }

        private IEnumerator<T> GetEnumeratorPrivate()
        {
            return new List<T>(this.queue).GetEnumerator();
        }

        private void Heapify()
        {
            for (int index = this.GetParentIndex(this.Count); index >= 0; --index)
            {
                this.DownHeap(index);
            }
        }

        private void DownHeap(int itemIndex)
        {
            while (itemIndex < this.queue.Count)
            {
                int smallestChildIndex = this.GetSmallestChildIndex(itemIndex);

                if (smallestChildIndex == itemIndex)
                {
                    break;
                }

                T item = this.queue[itemIndex];

                this.queue[itemIndex] = this.queue[smallestChildIndex];
                itemIndex = smallestChildIndex;
                this.queue[itemIndex] = item;
            }
        }

        private void UpHeap(int itemIndex)
        {
            while (itemIndex > 0)
            {
                int parentIndex = this.GetParentIndex(itemIndex);
                T parent = this.queue[parentIndex];

                T item = this.queue[itemIndex];

                if (this.comparer.Compare(item, parent) >= 0)
                {
                    break;
                }

                this.queue[itemIndex] = parent;
                itemIndex = parentIndex;
                this.queue[itemIndex] = item;
            }
        }

        private int GetSmallestChildIndex(int parentIndex)
        {
            int leftChildIndex = (parentIndex * 2) + 1;
            int rightChildIndex = leftChildIndex + 1;
            int smallestChildIndex = parentIndex;

            if (leftChildIndex < this.queue.Count
                && this.comparer.Compare(this.queue[smallestChildIndex], this.queue[leftChildIndex]) > 0)
            {
                smallestChildIndex = leftChildIndex;
            }

            if (rightChildIndex < this.queue.Count
                && this.comparer.Compare(this.queue[smallestChildIndex], this.queue[rightChildIndex]) > 0)
            {
                smallestChildIndex = rightChildIndex;
            }

            return smallestChildIndex;
        }

        private int GetParentIndex(int childIndex)
        {
            return (childIndex - 1) / 2;
        }
    }
}
