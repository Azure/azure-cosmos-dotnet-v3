// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.AsyncEnumerable
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal interface ITraceableAsyncEnumerator<out T> : IAsyncEnumerator<T>
    {
        ValueTask<bool> MoveNextAsync(ITrace trace);
    }
}
