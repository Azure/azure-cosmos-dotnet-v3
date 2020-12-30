//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class ClientEncryptionPropertiesRefreshManager : IDisposable
    {
        private readonly TimeSpan cekPropertiesRefreshTimeInterval;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly TimeSpan defaultCekPropertiesRefreshTimeInterval = TimeSpan.FromMinutes(30);
        private readonly EncryptionCosmosClient cosmosClient;
        private bool isRefreshing = false;
        private bool isDisposed = false;

        /// <summary>
        /// Runs in the background to update the Client Encryption Properties Cache.
        /// </summary>
        /// <param name="cosmosClient">Cosmos Client handler </param>
        /// <param name="cekPropertiesRefreshTimeInterval">Time interval between successive iterations of the background task to refresh.</param>
        public ClientEncryptionPropertiesRefreshManager(
            EncryptionCosmosClient cosmosClient,
            TimeSpan? cekPropertiesRefreshTimeInterval = null)
        {
            this.cosmosClient = cosmosClient;

            if (cekPropertiesRefreshTimeInterval.HasValue)
            {
                if (cekPropertiesRefreshTimeInterval.Value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(cekPropertiesRefreshTimeInterval));
                }

                this.cekPropertiesRefreshTimeInterval = cekPropertiesRefreshTimeInterval.Value;
            }
            else
            {
                this.cekPropertiesRefreshTimeInterval = this.defaultCekPropertiesRefreshTimeInterval;
            }

            this.StartCekPropertiesWatchDogTimer();
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void StartCekPropertiesWatchDogTimer()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await Task.Delay(this.cekPropertiesRefreshTimeInterval, this.cancellationTokenSource.Token);
                this.UpdateClientEncryptionProperties();
            }
            catch (Exception ex)
            {
                if (this.cancellationTokenSource.IsCancellationRequested &&
                    (ex is TaskCanceledException || ex is ObjectDisposedException))
                {
                    return;
                }

                this.StartCekPropertiesWatchDogTimer();
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void UpdateClientEncryptionProperties()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (!this.isRefreshing && !this.isDisposed)
            {
                HashSet<string> encryptedDatabaseList = new HashSet<string>(this.cosmosClient.GetEncryptedDatabaseIds());
                foreach (string databaseId in encryptedDatabaseList)
                {
                    try
                    {
                        Database database = this.cosmosClient.GetDatabase(databaseId);
                        this.isRefreshing = true;

                        using (FeedIterator<ClientEncryptionKeyProperties> feedIterator = database.GetClientEncryptionKeyIterator(null))
                        {
                            while (feedIterator.HasMoreResults)
                            {
                                FeedResponse<ClientEncryptionKeyProperties> feedResponse = await feedIterator.ReadNextAsync();
                                foreach (ClientEncryptionKeyProperties clientEncryptionKeyProperties in feedResponse.Resource)
                                {
                                    await this.cosmosClient.UpdateClientEncryptionPropertyCacheAsync(
                                                clientEncryptionKeyProperties.Id,
                                                database.Id,
                                                clientEncryptionKeyProperties);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // can happen before we set a cancellation.
                        if (ex is ObjectDisposedException)
                        {
                            return;
                        }
                    }
                }

                this.isRefreshing = false;
            }

            this.StartCekPropertiesWatchDogTimer();
            return;
        }

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                if (!this.cancellationTokenSource.IsCancellationRequested)
                {
                    this.cancellationTokenSource.Cancel();

                    this.cancellationTokenSource.Dispose();
                }

                this.isDisposed = true;
            }
        }
    }
}
