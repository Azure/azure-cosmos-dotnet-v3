// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class TracingAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly ITracingAsyncEnumerator<T> enumerator;
        private readonly ITrace trace;

        public TracingAsyncEnumerator(ITracingAsyncEnumerator<T> enumerator, ITrace trace)
        {
            this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        public T Current => this.enumerator.Current;

        public ValueTask DisposeAsync()
        {
            return this.enumerator.DisposeAsync();
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return this.enumerator.MoveNextAsync(this.trace);
        }
    }
}