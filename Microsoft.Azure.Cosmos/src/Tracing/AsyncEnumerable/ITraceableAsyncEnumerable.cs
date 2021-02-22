// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.AsyncEnumerable
{
    using System.Collections.Generic;
    using System.Threading;

    internal interface ITraceableAsyncEnumerable<out T> : IAsyncEnumerable<T>
    {
        ITraceableAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken, ITrace trace);
    }
}
