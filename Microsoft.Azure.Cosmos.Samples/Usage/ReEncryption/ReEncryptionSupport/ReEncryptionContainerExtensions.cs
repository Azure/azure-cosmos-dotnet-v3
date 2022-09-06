//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Cosmos.Samples.ReEncryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// This class provides extension methods for <see cref="Container"/>.
    /// </summary>
    public static class ReEncryptionContainerExtension
    {
        /// <summary>
        /// Gets an iterator for reencrypting the data.
        /// The source container should have no data changes during reEncryption operation or should have changefeed full fidelity enabled.
        /// </summary>
        /// <param name="container"> Source container object. </param>
        /// <param name="destinationContainerName"> Destination Container configured with new policy or key. </param>
        /// <param name="checkIfWritesHaveStoppedCb"> Callback to check if writes have stopped. The called function should return true if writes have stopped. If FullFidelity change feed is not enabled, return true by default. </param>
        /// <param name="changeFeedRequestOptions"> (Optional) Request options. </param>
        /// <param name="sourceFeedRange"> (Optional) The range to start from. </param>
        /// <param name="continuationToken"> (Optional) continuationToken: The continuation to resume from. </param>
        /// <param name="cancellationToken"> (Optional) System.Threading.CancellationToken representing request cancellation. </param>
        /// <returns> Returns a ReEncryption Iterator. </returns>
        public static async Task<ReEncryptionIterator> GetReEncryptionIteratorAsync(
            this Container container,
            string destinationContainerName,
            CosmosClient encryptionCosmosClient,
            Func<bool> checkIfWritesHaveStoppedCb,
            ChangeFeedRequestOptions changeFeedRequestOptions = null,
            FeedRange sourceFeedRange = null,
            string continuationToken = null,
            CancellationToken cancellationToken = default)
        {
            if (checkIfWritesHaveStoppedCb == null)
            {
                throw new ArgumentNullException(nameof(checkIfWritesHaveStoppedCb));
            }

            if (string.IsNullOrEmpty(destinationContainerName))
            {
                throw new ArgumentNullException(nameof(destinationContainerName));
            }

            if (!encryptionCosmosClient.ClientOptions.AllowBulkExecution)
            {
                throw new NotSupportedException("GetReEncryptionIteratorAsync requires client to be enabled with Bulk Execution. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }

            Container destContainer = encryptionCosmosClient.GetContainer(container.Database.Id, destinationContainerName);
            ContainerProperties containerProperties = await container.ReadContainerAsync(cancellationToken: cancellationToken);

            if (containerProperties.ChangeFeedPolicy.FullFidelityRetention == TimeSpan.Zero && Constants.IsFFChangeFeedSupported)
            {
                throw new NotSupportedException("GetReEncryptionIteratorAsync requires container to be enabled with FullFidelity ChangeFeedPolicy. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }

            ReEncryptionIterator reEncryptionIterator = new ReEncryptionIterator(
                container,
                destContainer,
                containerProperties.PartitionKeyPath,
                sourceFeedRange,
                changeFeedRequestOptions,
                continuationToken,
                checkIfWritesHaveStoppedCb,
                isFFChangeFeedSupported: Constants.IsFFChangeFeedSupported);

            return reEncryptionIterator;
        }
    }
}
