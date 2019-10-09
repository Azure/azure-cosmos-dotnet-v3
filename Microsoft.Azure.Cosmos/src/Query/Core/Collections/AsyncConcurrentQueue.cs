//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Collections.Generic
{
    using System.Collections.Concurrent;

    internal sealed class AsyncConcurrentQueue<T> : AsyncCollectionBase<T>
    {
        private readonly ConcurrentQueue<T> concurrentQueue;

        public AsyncConcurrentQueue()
           : this(new ConcurrentQueue<T>(), int.MaxValue)
        {
        }

        public AsyncConcurrentQueue(int boundingCapacity)
            : this(new ConcurrentQueue<T>(), boundingCapacity)
        {
        }

        public AsyncConcurrentQueue(ConcurrentQueue<T> initialCollection)
          : this(initialCollection, int.MaxValue)
        {
        }

        public AsyncConcurrentQueue(ConcurrentQueue<T> initialCollection, int boundingCapacity)
            : base(initialCollection.Count, boundingCapacity)
        {
            this.concurrentQueue = initialCollection;
            base.collection = this.concurrentQueue;
        }

        public override bool TryPeek(out T item)
        {
            return this.concurrentQueue.TryPeek(out item);
        }
    }
}