//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    ///  A list that can grow only up to a specified capacity.
    ///  In the growth phase, it uses the standard List type.
    ///  At capacity, it switches to a circular queue implementation.
    /// </summary>
    internal sealed class BoundedList<T> : IEnumerable<T>
    {
        private readonly int capacity;

        private List<T> elementList;

        private CircularQueue<T> circularQueue;

        private BoundedList(int capacity)
        {
            this.capacity = capacity;
            this.elementList = new List<T>();
            this.circularQueue = null;
        }

        internal static bool TryCreate(int capacity, out BoundedList<T> boundedList)
        {
            boundedList = null;

            if (capacity > 0)
            {
                boundedList = new BoundedList<T>(capacity);
                return true;
            }

            return false;
        }

        public void Add(T element)
        {
            if (this.circularQueue != null)
            {
                this.circularQueue.Add(element);
            }
            else if (this.elementList.Count < this.capacity)
            {
                this.elementList.Add(element);
            }
            else
            {
                _ = CircularQueue<T>.TryCreate(this.capacity, out this.circularQueue);
                this.circularQueue.AddRange(this.elementList);
                this.elementList = null;
                this.circularQueue.Add(element);
            }
        }

        public void AddRange(IEnumerable<T> elements)
        {
            foreach (T element in elements)
            {
                this.Add(element);
            }
        }

        public IEnumerator<T> GetListEnumerator()
        {
            // Using a for loop with a yield prevents Issue #1467 which causes
            // ThrowInvalidOperationException if a new diagnostics is getting added
            // while the enumerator is being used.
            List<T> elements = this.elementList;
            for (int index = 0; index < elements.Count; ++index)
            {
                yield return elements[index];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (this.circularQueue != null)
            {
                return this.circularQueue.GetEnumerator();
            }
            else
            {
                return this.GetListEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
