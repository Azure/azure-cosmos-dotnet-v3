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
        private readonly Func<CancellationToken, Task<T>> valueFactory;
        private T value;

        public AsyncLazy(Func<CancellationToken, Task<T>> valueFactory)
        {
            if (valueFactory == null)
            {
                throw new ArgumentNullException(nameof(valueFactory));
            }

            this.valueFactory = valueFactory;
        }

        public bool ValueInitialized { get; private set; }

        public async Task<T> GetValueAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!this.ValueInitialized)
            {
                this.value = await this.valueFactory(cancellationToken);
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
