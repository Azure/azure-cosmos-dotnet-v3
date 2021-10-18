// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Reencrypts the data read from change feed.
    /// </summary>
    public sealed class ReencryptionIterator
    {
        private const int PageSize = 1000;

        private readonly Container destinationContainer;
        private readonly Container sourceContainer;
        private readonly string partitionKey;
        private readonly FeedRange feedRange;
        private readonly ReencryptionBulkOperationBuilder reencryptionBulkOperationBuilder;
        private readonly CheckIfWritesHaveStopped checkIfWritesHaveStoppedCb;
        private readonly bool isFFChangeFeedSupported;

        private FeedIterator feedIterator;

        private delegate bool CheckIfWritesHaveStopped();

        private ChangeFeedRequestOptions changeFeedRequestOptions;

        /// <summary>
        /// Gets continuation Token
        /// </summary>
        public string ContinuationToken { get; private set; }

        /// <summary>
        /// Gets a value indicating whether has More Results.
        /// </summary>
        public bool HasMoreResults { get; private set; }

        /// <summary>
        /// EncryptNextAsync.
        /// </summary>
        /// <param name="cancellationToken"> cancellationTOken </param>
        /// <returns> Response Message </returns>
        public async Task<ReencryptionResponseMessage> EncryptNextAsync(
            CancellationToken cancellationToken = default)
        {
            if (!this.checkIfWritesHaveStoppedCb() && this.isFFChangeFeedSupported == false)
            {
                throw new NotSupportedException("Reencryption currently supported only on container with no ongoing changes. To perform reencryption please make sure there is is no data being written to the container. ");
            }

            return await this.EncryptNextCoreAsync(cancellationToken);
        }

        /// <summary>
        /// Get Reencryption Feed Iterator.
        /// </summary>
        /// <param name="sourceContainer"> Source container. </param>
        /// <param name="destinationContainer"> Destination container. </param>
        /// <param name="partitionKey"> Partition Key. </param>
        /// <param name="sourceFeedRange"> Feed range. </param>
        /// <param name="continuationToken"> Continuation token. </param>
        /// <param name="checkIfWritesHaveStopped"> Callback to invoked to check if writes have stopped. </param>
        internal ReencryptionIterator(
            Container sourceContainer,
            Container destinationContainer,
            string partitionKey,
            FeedRange sourceFeedRange,
            ChangeFeedRequestOptions changeFeedRequestOptions,
            string continuationToken,
            Func<bool> checkIfWritesHaveStopped,
            bool isFFChangeFeedSupported = false)
        {
            this.sourceContainer = sourceContainer ?? throw new ArgumentNullException(nameof(sourceContainer));
            this.destinationContainer = destinationContainer ?? throw new ArgumentNullException(nameof(destinationContainer));
            this.partitionKey = string.IsNullOrEmpty(partitionKey) ? throw new ArgumentNullException(nameof(partitionKey)) : partitionKey.Substring(1);
            this.feedRange = sourceFeedRange;
            this.changeFeedRequestOptions = changeFeedRequestOptions;
            this.reencryptionBulkOperationBuilder = new ReencryptionBulkOperationBuilder(destinationContainer, partitionKey);
            this.HasMoreResults = true;
            this.ContinuationToken = continuationToken;
            this.checkIfWritesHaveStoppedCb = new CheckIfWritesHaveStopped(checkIfWritesHaveStopped);
            this.isFFChangeFeedSupported = isFFChangeFeedSupported;
        }

        private async Task<ReencryptionResponseMessage> EncryptNextCoreAsync(
            CancellationToken cancellationToken)
        {
            (string continuationToken, string fullFidelityStartLsn) = await this.ParseContinuationTokenAsync(this.ContinuationToken);

            bool isFullSyncRequired = false;

            if (this.isFFChangeFeedSupported)
            {
                if (this.CheckIfFullSyncIsRequired(fullFidelityStartLsn, continuationToken))
                {
                    isFullSyncRequired = true;
                }
            }
            else
            {
                isFullSyncRequired = true;
            }

            this.feedIterator = this.SetChangeFeedIterator(continuationToken, isFullSyncRequired);

            ResponseMessage responseMessage = null;
            ReencryptionBulkOperationResponse<JObject> reencryptionBulkOperationResponse = null;
            if (isFullSyncRequired)
            {
                (responseMessage, reencryptionBulkOperationResponse, continuationToken) = await this.InitiateFullSyncAsync(
                    fullFidelityStartLsn,
                    cancellationToken);
            }
            else if (this.isFFChangeFeedSupported)
            {
                 (responseMessage, reencryptionBulkOperationResponse, continuationToken) = await this.GetAndReencryptFFChangesAsync(cancellationToken);
            }

            if (responseMessage.StatusCode == HttpStatusCode.OK || responseMessage.StatusCode == HttpStatusCode.NotModified)
            {
                this.ContinuationToken = BuildModifiedContinuationToken(
                    continuationToken,
                    fullFidelityStartLsn);
            }

            return new ReencryptionResponseMessage(responseMessage, this.ContinuationToken, reencryptionBulkOperationResponse);
        }

        private async Task<(ResponseMessage, ReencryptionBulkOperationResponse<JObject>, string)> InitiateFullSyncAsync(
            string fullFidelityStartLSNString,
            CancellationToken cancellationToken)
        {
            long currentDrainedLSN = 0;
            long fullFidelityStartLSN = 0;
            string currentDrainedLSNString = null;
            ReencryptionBulkOperationResponse<JObject> bulkOperationResponse = null;
            ResponseMessage response = await this.feedIterator.ReadNextAsync(cancellationToken: cancellationToken);
            string continuationToken = response.ContinuationToken;
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                if (this.checkIfWritesHaveStoppedCb() && !this.isFFChangeFeedSupported)
                {
                    this.StoppedWrites();
                }

                return (response, bulkOperationResponse, continuationToken);
            }
            else
            {
                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    JObject contentJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(response.Content);
                    if (!(contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents))
                    {
                        throw new InvalidOperationException("Feed Response body contract was violated. Feed response did not have an array of Documents. ");
                    }

                    ReencryptionBulkOperations<JObject> bulkOperations = new ReencryptionBulkOperations<JObject>(documents.Count);
                    foreach (JToken value in documents)
                    {
                        if (value is not JObject document)
                        {
                            continue;
                        }

                        currentDrainedLSNString = document.GetValue("_lsn").ToString();
                        currentDrainedLSN = long.Parse(currentDrainedLSNString);
                        fullFidelityStartLSN = long.Parse(fullFidelityStartLSNString);

                        document.Remove("_lsn");
                        bulkOperations.Tasks.Add(this.destinationContainer.UpsertItemAsync(
                            item: document,
                            new PartitionKey(document.GetValue(this.partitionKey).ToString()),
                            cancellationToken: default).CaptureReencryptionOperationResponseAsync(document));
                    }

                    bulkOperationResponse = await bulkOperations.ExecuteAsync();
                    if (bulkOperationResponse.FailedDocuments.Count > 0)
                    {
                        // just send one of the failure response.
                        response = new ResponseMessage(
                            (HttpStatusCode)207, // MultiStatus
                            response.RequestMessage,
                            "Reencryption Operation failed. Please go through ReencryptionBulkOperationResponse for details regarding failed operations. ");
                        return (response, bulkOperationResponse, continuationToken);
                    }

                    // read out all the changes in the page. Breaking in between can lead to problems if we switch, when there are multiple
                    // changes with same LSN due to, say a batch operation and we would end up missing it in Full Fidelity.
                    // For an LSN all changes corresponding to it will be returned in the same page.
                    if (currentDrainedLSN >= fullFidelityStartLSN)
                    {
                        string lsnToReplace = this.GetLsnFromContinuationString(continuationToken);
                        continuationToken = continuationToken.Replace(lsnToReplace, currentDrainedLSNString);
                        return (response, bulkOperationResponse, continuationToken);
                    }
                }
            }

            return (response, bulkOperationResponse, continuationToken);
        }

        private async Task<(ResponseMessage, ReencryptionBulkOperationResponse<JObject>, string)> GetAndReencryptFFChangesAsync(
            CancellationToken cancellationToken)
        {
            ReencryptionBulkOperationResponse<JObject> bulkOperationResponse = null;
            ResponseMessage response = await this.feedIterator.ReadNextAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                if (this.checkIfWritesHaveStoppedCb())
                {
                    this.StoppedWrites();
                }

                return (response, bulkOperationResponse, response.ContinuationToken);
            }

            if (response.IsSuccessStatusCode && response.Content != null)
            {
                bulkOperationResponse = await this.reencryptionBulkOperationBuilder.ExecuteAsync(response, cancellationToken);

                if (bulkOperationResponse.FailedDocuments.Count > 0)
                {
                    foreach ((JObject, Exception) failedOperation in bulkOperationResponse.FailedDocuments)
                    {
                        CosmosException ex = (CosmosException)failedOperation.Item2;
                        if (ex.StatusCode == HttpStatusCode.NotFound)
                        {
                            JObject metadata = failedOperation.Item1.GetValue("_metadata").ToObject<JObject>();
                            string operationType = metadata.GetValue("operationType").ToString();
                            if (!operationType.Equals("delete"))
                            {
                                response = new ResponseMessage(
                                    (HttpStatusCode)207, // MultiStatus
                                    response.RequestMessage,
                                    "Reencryption Operation failed. Please go through ReencryptionBulkOperationResponse for details regarding failed operations. ");
                                return (response, bulkOperationResponse, response.ContinuationToken);
                            }
                        }
                        else
                        {
                            response = new ResponseMessage(
                                HttpStatusCode.InternalServerError,
                                response.RequestMessage,
                                "Reencryption Operation failed. Please go through ReencryptionBulkOperationResponse for details regarding failed operations. ");
                            return (response, bulkOperationResponse, response.ContinuationToken);
                        }
                    }
                }
            }

            return (response, bulkOperationResponse, response.ContinuationToken);
        }

        private FeedIterator SetChangeFeedIterator(
            string continuationToken,
            bool isFullSyncRequired)
        {
            ChangeFeedStartFrom changeFeedStartFrom;

            if (this.changeFeedRequestOptions == null)
            {
                this.changeFeedRequestOptions = new ChangeFeedRequestOptions
                {
                    PageSizeHint = PageSize,
                };
            }

            FeedIterator feedIterator = null;

            if (isFullSyncRequired)
            {
                changeFeedStartFrom = string.IsNullOrEmpty(continuationToken) ? ((this.feedRange == null) ? ChangeFeedStartFrom.Beginning() : ChangeFeedStartFrom.Beginning(this.feedRange)) : ChangeFeedStartFrom.ContinuationToken(continuationToken);
                feedIterator = this.sourceContainer
                    .GetChangeFeedStreamIterator(
                    changeFeedStartFrom,
                    ChangeFeedMode.Incremental,
                    this.changeFeedRequestOptions);
            }
            else if (this.isFFChangeFeedSupported)
            {
                if (string.IsNullOrEmpty(continuationToken))
                {
                    throw new ArgumentException("TransferDataWithReencryptionAsync requires a valid continuation token for getting FullFidelity change feed. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
                }

                feedIterator = this.sourceContainer
                    .GetChangeFeedStreamIterator(
                    ChangeFeedStartFrom.ContinuationToken(continuationToken),
                    ChangeFeedMode.FullFidelity,
                    this.changeFeedRequestOptions);
            }

            return feedIterator;
        }

        private void StoppedWrites()
        {
            this.HasMoreResults = false;
        }

        private bool CheckIfFullSyncIsRequired(string fullFidelityStartLsn, string continuationToken)
        {
            if (string.IsNullOrEmpty(fullFidelityStartLsn) || string.IsNullOrEmpty(continuationToken))
            {
                return true;
            }

            string currentLsn = this.GetLsnFromContinuationString(continuationToken);

            if (string.IsNullOrEmpty(currentLsn))
            {
                throw new InvalidOperationException("Failed to fetch currentLsn value. ");
            }

            long currentLsnLong = long.Parse(currentLsn);
            long fullFidelityStartLsnLong = long.Parse(fullFidelityStartLsn);

            if (currentLsnLong >= fullFidelityStartLsnLong)
            {
                return false;
            }

            return true;
        }

        private async Task<string> GetCurrentLargestCommittedLsnAsync()
        {
            ChangeFeedRequestOptions changeFeedRequestOptions = new ChangeFeedRequestOptions
            {
                PageSizeHint = 1,
            };

            ChangeFeedStartFrom changeFeedStartFrom = (this.feedRange == null) ? ChangeFeedStartFrom.Now() : ChangeFeedStartFrom.Now(this.feedRange);
            FeedIterator iterator = this.sourceContainer.GetChangeFeedStreamIterator(
                changeFeedStartFrom,
                ChangeFeedMode.Incremental,
                changeFeedRequestOptions);

            ResponseMessage response = await iterator.ReadNextAsync();

            return this.GetLsnFromContinuationString(response.ContinuationToken);
        }

        private static string BuildModifiedContinuationToken(string continuationToken, string fullFidelityStartLsn)
        {
            if (!string.IsNullOrEmpty(continuationToken) && !string.IsNullOrEmpty(fullFidelityStartLsn))
            {
                JObject jContinuationToken = new JObject
                {
                    { "continuationToken", continuationToken },
                    { "fullFidelityStartLsn", fullFidelityStartLsn },
                };
                return jContinuationToken.ToString();
            }
            else
            {
                throw new ArgumentNullException("Failed to create continuation token. ");
            }
        }

        private async Task<(string, string)> ParseContinuationTokenAsync(string continuationToken)
        {
            if (string.IsNullOrEmpty(continuationToken))
            {
                string fullFidelityStartLSNString = await this.GetCurrentLargestCommittedLsnAsync();
                return (null, fullFidelityStartLSNString);
            }

            JObject jContinuationToken = JObject.Parse(continuationToken);
            return (jContinuationToken.Value<string>("continuationToken"), jContinuationToken.Value<string>("fullFidelityStartLsn"));
        }

        private string GetLsnFromContinuationString(string continuationString)
        {
            JToken parseContinuation = JObject.Parse(continuationString).GetValue("Continuation");
            string fullFidelityStartLSN = null;
            if (parseContinuation is JArray continuationArray)
            {
                if (continuationArray.Count != 1 && this.feedRange != null)
                {
                    throw new Exception($"GetFullFidelityStartLSNAsync - Invalid continuation token :{continuationString}");
                }

                foreach (JToken jToken in continuationArray)
                {
                    fullFidelityStartLSN = jToken.SelectToken("State").SelectToken("value").Value<string>().Trim('"');
                }
            }

            return fullFidelityStartLSN;
        }
    }
}