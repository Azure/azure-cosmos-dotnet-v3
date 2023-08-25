// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class AsyncLazy<T>
    {
        private readonly Func<ITrace, CancellationToken, Task<T>> valueFactory;
        private T value;

        public AsyncLazy(Func<ITrace, CancellationToken, Task<T>> valueFactory)
        {
            this.valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        }

        public bool ValueInitialized { get; private set; }

        public async Task<T> GetValueAsync(ITrace trace, CancellationToken cancellationToken)
        {
            // Note that this class is not thread safe.
            // if the valueFactory has side effects than this will have issues.
            if (!this.ValueInitialized)
            {
                this.value = await this.valueFactory(trace, cancellationToken);
                this.ValueInitialized = true;
            }

            return this.value;
        }

        public T Result
        {
            get
            {
                if (!this.ValueInitialized)
                {
                    throw new InvalidOperationException("Can not retrieve value before initialization.");
                }

                return this.value;
            }
        }
    }
}
