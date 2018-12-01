//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Common
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class AsyncLazy<T> : Lazy<Task<T>>
    {
        public AsyncLazy(Func<T> valueFactory, CancellationToken cancellationToken) :
            base(() => Task.Factory.StartNew(valueFactory, cancellationToken)) { }

        public AsyncLazy(Func<Task<T>> taskFactory, CancellationToken cancellationToken) :
            base(() => Task.Factory.StartNew(taskFactory, cancellationToken).Unwrap()) { }

        /// <summary>
        /// True if value initialization failed or was cancelled.
        /// </summary>
        public bool IsFaultedOrCancelled 
        {
            get
            {
                return this.Value.IsCanceled || this.Value.IsFaulted;
            }
        }

        /// <summary>
        /// True if value is initialized - either successfully or unsuccessfully.
        /// </summary>
        public bool IsCompleted
        {
            get
            {
                return this.Value.IsCompleted;
            }
        }

        public TaskAwaiter<T> GetAwaiter() { return this.Value.GetAwaiter(); }
    }
}
