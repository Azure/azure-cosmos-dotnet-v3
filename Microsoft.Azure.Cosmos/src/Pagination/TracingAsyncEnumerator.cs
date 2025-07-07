// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class TracingAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly ITracingAsyncEnumerator<T> enumerator;
        private readonly ITrace trace;
        private readonly CancellationToken cancellationToken;

        public TracingAsyncEnumerator(ITracingAsyncEnumerator<T> enumerator, ITrace trace, CancellationToken cancellationToken)
        {
            this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
            this.cancellationToken = cancellationToken;
        }

        public T Current => this.enumerator.Current;

        public ValueTask DisposeAsync()
        {
            return this.enumerator.DisposeAsync();
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return this.enumerator.MoveNextAsync(this.trace, this.cancellationToken);
        }
    }
}