// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;

    internal interface IQueue<T> : IEnumerable<T>
    {
        T Peek();

        void Enqueue(T item);

        T Dequeue();

        public int Count { get; }
    }
}
