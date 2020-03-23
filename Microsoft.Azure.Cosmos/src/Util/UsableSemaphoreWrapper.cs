//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;

    internal sealed class UsableSemaphoreWrapper : IDisposable
    {
        private readonly SemaphoreSlim semaphore;
        private bool disposed;
        public UsableSemaphoreWrapper(SemaphoreSlim semaphore)
        {
            this.semaphore = semaphore;
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.semaphore.Release();
            this.disposed = true;
        }
    }
}
