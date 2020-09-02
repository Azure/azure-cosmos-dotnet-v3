//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Implementation of <see cref="DocumentServiceLeaseStore"/> for state in-memory
    /// </summary>
    internal sealed class DocumentServiceLeaseStoreInMemory : DocumentServiceLeaseStore
    {
        private bool isInitialized = false;

        public DocumentServiceLeaseStoreInMemory()
        {
        }

        public override Task<bool> IsInitializedAsync()
        {
            return Task.FromResult(this.isInitialized);
        }

        public override Task MarkInitializedAsync()
        {
            this.isInitialized = true;
            return Task.CompletedTask;
        }

        public override Task<bool> AcquireInitializationLockAsync(TimeSpan lockTime)
        {
            return Task.FromResult(true);
        }

        public override Task<bool> ReleaseInitializationLockAsync()
        {
            return Task.FromResult(true);
        }
    }
}