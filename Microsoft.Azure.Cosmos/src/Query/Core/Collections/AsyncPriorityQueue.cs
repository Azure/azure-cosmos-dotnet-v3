//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Collections.Generic
{
    internal sealed class AsyncPriorityQueue<T> : AsyncCollectionBase<T>
    {
        private readonly PriorityQueue<T> priorityQueue;

        public AsyncPriorityQueue()
           : this(new PriorityQueue<T>(), int.MaxValue)
        {
        }

        public AsyncPriorityQueue(int boundingCapacity)
            : this(new PriorityQueue<T>(), boundingCapacity)
        {
        }

        public AsyncPriorityQueue(PriorityQueue<T> initialCollection)
          : this(initialCollection, int.MaxValue)
        {
        }

        public AsyncPriorityQueue(PriorityQueue<T> initialCollection, int boundingCapacity)
            : base(initialCollection.Count, boundingCapacity)
        {
            this.priorityQueue = initialCollection;
            base.collection = this.priorityQueue;
        }

        public override bool TryPeek(out T item)
        {
            return this.priorityQueue.TryPeek(out item);
        }
    }
}