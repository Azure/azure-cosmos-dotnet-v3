//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;

    internal class UsableSemaphoreWrapper : IDisposable
    {
        private readonly SemaphoreSlim semaphore;
        private bool diposed;
        public UsableSemaphoreWrapper(SemaphoreSlim semaphore)
        {
            this.semaphore = semaphore;
        }

        public void Dispose()
        {
            if (this.diposed)
            {
                return;
            }

            this.semaphore.Release();
            this.diposed = true;
        }
    }
}
