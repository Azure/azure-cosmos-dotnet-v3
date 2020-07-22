//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Timers;

    internal sealed class UnwrappedDekLifecycleManager : IDisposable
    {
        private readonly CosmosDataEncryptionKeyProvider dekProvider;
        private readonly TimeSpan iterationInterval = TimeSpan.FromMinutes(5);
        private readonly Timer timer;
        private readonly List<InMemoryRawDek> inMemoryRawDeks;
        private bool isRunning = false;
        private bool isDisposed = false;

        /// <summary>
        /// Kick-start a background task which runs periodically to manage lifecycle of the unwrapped DEKs by doing the following:
        /// 1. Refresh unwrapped DEK and extend TimeToLive.
        /// 2. Delete (dispose) unwrapped DEK from memory after client specified TimeToLive expires or if it hasn't been used lately.
        /// </summary>
        /// <param name="dekProvider">Cosmos DEK provider to help manage the DEK lifecycle.</param>
        /// <param name="iterationInterval">Time interval between successive runs.</param>
        public UnwrappedDekLifecycleManager(
            CosmosDataEncryptionKeyProvider dekProvider,
            TimeSpan? iterationInterval = null)
        {
            this.dekProvider = dekProvider;

            if (iterationInterval.HasValue)
            {
                if (iterationInterval.Value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(iterationInterval));
                }

                this.iterationInterval = iterationInterval.Value;
            }

            this.inMemoryRawDeks = new List<InMemoryRawDek>();

            this.timer = new Timer(this.iterationInterval.TotalMilliseconds);
            this.timer.Elapsed += this.Run;
            this.timer.AutoReset = true;
            this.timer.Enabled = true;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Background task.")]
        private void Run(object source, ElapsedEventArgs e)
        {
            if (!this.isRunning)
            {
                this.isRunning = true;
                for (int index = 0; index < this.inMemoryRawDeks.Count; index++)
                {
                    if (this.inMemoryRawDeks[index].RawDekExpiry <= DateTime.UtcNow)
                    {
                        this.inMemoryRawDeks[index].DataEncryptionKey.Dispose();
                        this.inMemoryRawDeks.RemoveAt(index--);
                    }
                    else if (this.inMemoryRawDeks[index].IsRefreshNeeded())
                    {
                        EncryptionKeyUnwrapResult unwrapResult;
                        try
                        {
                            unwrapResult = this.dekProvider.EncryptionKeyWrapProvider.UnwrapKeyAsync(
                                this.inMemoryRawDeks[index].DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                                this.inMemoryRawDeks[index].DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                                cancellationToken: default).Result;
                        }
                        catch (Exception)
                        {
                            this.inMemoryRawDeks[index].UpdateNextRefreshTime();
                            continue;
                        }

                        this.inMemoryRawDeks[index].RefreshTimeToLive(unwrapResult.ClientCacheTimeToLive);
                        this.dekProvider.DekCache.SetRawDek(
                            this.inMemoryRawDeks[index].DataEncryptionKeyProperties.Id,
                            this.inMemoryRawDeks[index]);
                    }
                }

                this.isRunning = false;
            }
        }

        public void Add(InMemoryRawDek inMemoryRawDek)
        {
            this.inMemoryRawDeks.Add(inMemoryRawDek);
        }

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.inMemoryRawDeks.Clear();
                this.timer.Stop();
                this.timer.Dispose();
                this.isDisposed = true;
            }
        }
    }
}