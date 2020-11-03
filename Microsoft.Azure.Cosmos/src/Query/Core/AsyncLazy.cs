// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class AsyncLazy<T>
    {
        private Func<CancellationToken, Task<T>> valueFactory;
        private T value;

        public AsyncLazy(Func<CancellationToken, Task<T>> valueFactory)
        {
            this.valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        }

        public bool ValueInitialized => this.valueFactory is null;

        public async ValueTask<T> GetValueAsync(CancellationToken cancellationToken)
        {
            // Note that this class is not thread safe.
            // if the valueFactory has side effects than this will have issues.
            cancellationToken.ThrowIfCancellationRequested();
            if (this.valueFactory is { } factory)
            {
                this.value = await factory(cancellationToken);
                this.valueFactory = null;
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
