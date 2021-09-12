// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Reactive
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Emits a particular item (or series of items).
    /// </summary>
    /// <typeparam name="T">The type of the item(s)</typeparam>
    /// <seealso href="http://reactivex.io/documentation/operators/just.html"/>
    internal sealed class JustAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> enumerator;

        public JustAsyncEnumerator(params T[] items)
        {
            this.enumerator = items.ToList().GetEnumerator();
        }

        public T Current => this.enumerator.Current;

        public ValueTask DisposeAsync() => default;

        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(this.enumerator.MoveNext());

        public ValueTask<bool> MoveNextAsync(ITrace trace) => new ValueTask<bool>(this.enumerator.MoveNext());
    }
}
