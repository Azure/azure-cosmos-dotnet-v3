// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Reactive
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Emits no items but terminates normally.
    /// </summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <seealso href="http://reactivex.io/documentation/operators/empty-never-throw.html"/>
    internal sealed class EmptyAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        public EmptyAsyncEnumerator()
        {
        }

        public T Current => default;

        public ValueTask DisposeAsync() => default;

        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(false);
    }
}
