//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;

    internal sealed class UnwrappedDekLifecycleManager : IDisposable
    {
        private readonly TimeSpan backgroundRefreshTimeInterval;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly TimeSpan defaultBackgroundRefreshTimeInterval = TimeSpan.FromMinutes(5);
        private readonly CosmosDataEncryptionKeyProvider dekProvider;
        private readonly List<InMemoryRawDek> inMemoryRawDeks;
        private bool isRefreshing = false;
        private bool isDisposed = false;

        /// <summary>
        /// Kick-start a background task which runs periodically to manage lifecycle of the unwrapped DEKs by doing the following:
        /// 1. Refresh unwrapped DEK and extend TimeToLive.
        /// 2. Delete (dispose) unwrapped DEK from memory after client specified TimeToLive expires or if it hasn't been used lately.
        /// </summary>
        /// <param name="dekProvider">Cosmos DEK provider to help manage the DEK lifecycle.</param>
        /// <param name="backgroundRefreshTimeInterval">Time interval between successive iterations of the background task to refresh DEKs.</param>
        public UnwrappedDekLifecycleManager(
            CosmosDataEncryptionKeyProvider dekProvider,
            TimeSpan? backgroundRefreshTimeInterval = null)
        {
            this.dekProvider = dekProvider;

            if (backgroundRefreshTimeInterval.HasValue)
            {
                if (backgroundRefreshTimeInterval.Value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(backgroundRefreshTimeInterval));
                }

                this.backgroundRefreshTimeInterval = backgroundRefreshTimeInterval.Value;
            }
            else
            {
                this.backgroundRefreshTimeInterval = this.defaultBackgroundRefreshTimeInterval;
            }

            this.inMemoryRawDeks = new List<InMemoryRawDek>();
            this.StartRefreshDekTimer();
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void StartRefreshDekTimer()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await Task.Delay(this.backgroundRefreshTimeInterval, this.cancellationTokenSource.Token);
                this.RefreshDek();
            }
            catch (Exception ex)
            {
                if (this.cancellationTokenSource.IsCancellationRequested &&
                    (ex is TaskCanceledException || ex is ObjectDisposedException))
                {
                    return;
                }

                this.StartRefreshDekTimer();
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void RefreshDek()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (!this.isRefreshing)
            {
                this.isRefreshing = true;
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
                            unwrapResult = await this.dekProvider.EncryptionKeyWrapProvider.UnwrapKeyAsync(
                                this.inMemoryRawDeks[index].DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                                this.inMemoryRawDeks[index].DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                                cancellationToken: default);
                        }
                        catch (Exception exception)
                        {
                            // If key access is removed, then remove the unwrapped key from cache and dispose
                            if (exception is RequestFailedException requestFailedException &&
                                requestFailedException.Status == 403)
                            {
                                this.inMemoryRawDeks[index].DataEncryptionKey.Dispose();
                                this.inMemoryRawDeks.RemoveAt(index--);
                            }
                            else
                            {
                                this.inMemoryRawDeks[index].UpdateNextRefreshTime();
                            }

                            continue;
                        }

                        this.inMemoryRawDeks[index].RefreshTimeToLive(unwrapResult.ClientCacheTimeToLive);
                        this.dekProvider.DekCache.SetRawDek(
                            this.inMemoryRawDeks[index].DataEncryptionKeyProperties.Id,
                            this.inMemoryRawDeks[index]);
                    }
                }

                this.isRefreshing = false;
            }

            this.StartRefreshDekTimer();
            return;
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
                if (!this.cancellationTokenSource.IsCancellationRequested)
                {
                    // If the user disposes of the object while awaiting an async call, this can cause task canceled exceptions.
                    this.cancellationTokenSource.Cancel();

                    // The background timer task can hit a ObjectDisposedException but it's an async background task
                    // that is never awaited on so it will not be thrown back to the caller.
                    this.cancellationTokenSource.Dispose();
                }

                this.isDisposed = true;
            }
        }
    }
}