// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.AsyncEnumerable
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
        interface ITraceableAsyncEnumerator<out T> : IAsyncEnumerator<T>
    {
        ValueTask<bool> MoveNextAsync(ITrace trace);
    }
}
