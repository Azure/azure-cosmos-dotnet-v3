//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class AsyncLazy<T> : Lazy<Task<T>>
    {
        public AsyncLazy(T value)
            : base(() => Task.FromResult(value))
        {
        }

        public AsyncLazy(Func<T> valueFactory, CancellationToken cancellationToken)
            : base(() => Task.Factory.StartNewOnCurrentTaskSchedulerAsync(valueFactory, cancellationToken)) // Task.Factory.StartNew() allows specifying task scheduler to use which is critical for compute gateway to track physical consumption.
        {
        }

        public AsyncLazy(Func<Task<T>> taskFactory, CancellationToken cancellationToken)
            : base(() => Task.Factory.StartNewOnCurrentTaskSchedulerAsync(taskFactory, cancellationToken).Unwrap()) // Task.Factory.StartNew() allows specifying task scheduler to use which is critical for compute gateway to track physical consumption.
        {
        }
    }
}