// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Cosmos.Samples.ReEncryption
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Reencrypts the data read from change feed.
    /// </summary>
    public sealed class ReEncryptionIterator
    {
        private const int PageSize = 1000;

        private readonly Container destinationContainer;
        private readonly Container sourceContainer;
        private readonly string partitionKeyPath;
        private readonly FeedRange feedRange;
        private readonly ReEncryptionBulkOperationBuilder reEncryptionBulkOperationBuilder;
        private readonly CheckIfWritesHaveStopped checkIfWritesHaveStoppedCb;
        private readonly bool isFullFidelityChangeFeedSupported;
        private readonly ReEncryptionJsonSerializer reEncryptionJsonSerializer;

        private FeedIterator feedIterator;

        private ChangeFeedRequestOptions changeFeedRequestOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReEncryptionIterator"/> class.
        /// Get ReEncryption Feed Iterator.
        /// </summary>
        /// <param name="sourceContainer"> Source container. </param>
        /// <param name="destinationContainer"> Destination container. </param>
        /// <param name="partitionKeyPath"> Partition key path. </param>
        /// <param name="sourceFeedRange"> Feed range. </param>
        /// <param name="continuationToken"> Continuation token. </param>
        /// <param name="checkIfWritesHaveStopped"> Callback to invoked to check if writes have stopped. </param>
        internal ReEncryptionIterator(
            Container sourceContainer,
            Container destinationContainer,
            string partitionKeyPath,
            FeedRange sourceFeedRange,
            ChangeFeedRequestOptions changeFeedRequestOptions,
            string continuationToken,
            Func<bool> checkIfWritesHaveStopped,
            bool isFFChangeFeedSupported = false)
        {
            this.sourceContainer = sourceContainer ?? throw new ArgumentNullException(nameof(sourceContainer));
            this.destinationContainer = destinationContainer ?? throw new ArgumentNullException(nameof(destinationContainer));
            this.partitionKeyPath = string.IsNullOrEmpty(partitionKeyPath) ? throw new ArgumentNullException(nameof(partitionKeyPath)) : partitionKeyPath[1..];
            this.feedRange = sourceFeedRange;
            this.changeFeedRequestOptions = changeFeedRequestOptions;
            this.reEncryptionBulkOperationBuilder = new ReEncryptionBulkOperationBuilder(destinationContainer, partitionKeyPath);
            this.HasMoreResults = true;
            this.ContinuationToken = continuationToken;
            this.checkIfWritesHaveStoppedCb = new CheckIfWritesHaveStopped(checkIfWritesHaveStopped);
            this.isFullFidelityChangeFeedSupported = isFFChangeFeedSupported;
            this.reEncryptionJsonSerializer = new ReEncryptionJsonSerializer(
                new JsonSerializerSettings()
                {
                    DateParseHandling = DateParseHandling.None,
                });
        }

        private delegate bool CheckIfWritesHaveStopped();

        /// <summary>
        /// Gets continuation Token.
        /// </summary>
        public string ContinuationToken { get; private set; }

        /// <summary>
        /// Gets a value indicating whether has More Results.
        /// </summary>
        public bool HasMoreResults { get; private set; }

        /// <summary>
        /// EncryptNextAsync.
        /// </summary>
        /// <param name="cancellationToken"> cancellationTOken. </param>
        /// <returns> Response Message. </returns>
        public async Task<ReEncryptionResponseMessage> EncryptNextAsync(
            CancellationToken cancellationToken = default)
        {
            if (!this.checkIfWritesHaveStoppedCb() && this.isFullFidelityChangeFeedSupported == false)
            {
                throw new NotSupportedException("ReEncryption currently supported only on container with no ongoing changes. To perform reEncryption please make sure there is no data being written to the container. ");
            }

            return await this.EncryptNextCoreAsync(cancellationToken);
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

        private async Task<ReEncryptionResponseMessage> EncryptNextCoreAsync(
            CancellationToken cancellationToken)
        {
            (string continuationToken, string fullFidelityStartLsn) = await this.ParseContinuationTokenAsync(this.ContinuationToken);

            bool isFullSyncRequired = false;

            if (this.isFullFidelityChangeFeedSupported)
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

            ResponseMessage responseMessage;
            ReEncryptionBulkOperationResponse<JObject> reEncryptionBulkOperationResponse;
            if (isFullSyncRequired)
            {
                (responseMessage, reEncryptionBulkOperationResponse, continuationToken) = await this.InitiateFullSyncAsync(
                    fullFidelityStartLsn,
                    cancellationToken);
            }
            else
            {
                 (responseMessage, reEncryptionBulkOperationResponse, continuationToken) = await this.GetAndReencryptFFChangesAsync(cancellationToken);
            }

            if (responseMessage.StatusCode == HttpStatusCode.OK || responseMessage.StatusCode == HttpStatusCode.NotModified)
            {
                this.ContinuationToken = BuildModifiedContinuationToken(
                    continuationToken,
                    fullFidelityStartLsn);
            }

            return new ReEncryptionResponseMessage(responseMessage, this.ContinuationToken, reEncryptionBulkOperationResponse);
        }

        private async Task<(ResponseMessage, ReEncryptionBulkOperationResponse<JObject>, string)> InitiateFullSyncAsync(
            string fullFidelityStartLSNString,
            CancellationToken cancellationToken)
        {
            long currentDrainedLSN = 0;
            long fullFidelityStartLSN = 0;
            string currentDrainedLSNString = null;
            ReEncryptionBulkOperationResponse<JObject> bulkOperationResponse = null;
            ResponseMessage response = await this.feedIterator.ReadNextAsync(cancellationToken: cancellationToken);
            string continuationToken = response.ContinuationToken;           

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                if (this.checkIfWritesHaveStoppedCb() && !this.isFullFidelityChangeFeedSupported)
                {
                    this.StoppedWrites();
                }

                return (response, bulkOperationResponse, continuationToken);
            }
            else
            {
                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    JObject contentJObj = this.reEncryptionJsonSerializer.FromStream<JObject>(response.Content);
                    if (!(contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents))
                    {
                        throw new InvalidOperationException("Feed Response body contract was violated. Feed response did not have an array of Documents. ");
                    }

                    ReEncryptionBulkOperations<JObject> bulkOperations = new ReEncryptionBulkOperations<JObject>(documents.Count);
                    foreach (JToken value in documents)
                    {
                        if (value is not JObject document)
                        {
                            continue;
                        }

                        currentDrainedLSNString = document.GetValue(Constants.LsnPropertyName).ToString();
                        currentDrainedLSN = long.Parse(currentDrainedLSNString);
                        fullFidelityStartLSN = long.Parse(fullFidelityStartLSNString);

                        document.Remove(Constants.LsnPropertyName);
                        bulkOperations.Tasks.Add(this.destinationContainer.UpsertItemAsync(
                            item: document,
                            new PartitionKey(document.GetValue(this.partitionKeyPath).ToString()),
                            cancellationToken: default).CaptureReEncryptionOperationResponseAsync(document));
                    }

                    bulkOperationResponse = await bulkOperations.ExecuteAsync();
                    if (bulkOperationResponse.FailedDocuments.Count > 0)
                    {
                        response = new ResponseMessage(
                            (HttpStatusCode)207, // MultiStatus
                            response.RequestMessage,
                            "ReEncryption Operation failed. Please go through ReEncryptionBulkOperationResponse for details regarding failed operations. ");
                        return (response, bulkOperationResponse, continuationToken);
                    }
                }
            }

            return (response, bulkOperationResponse, continuationToken);
        }

        private async Task<(ResponseMessage, ReEncryptionBulkOperationResponse<JObject>, string)> GetAndReencryptFFChangesAsync(
            CancellationToken cancellationToken)
        {
            ReEncryptionBulkOperationResponse<JObject> bulkOperationResponse = null;
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
                bulkOperationResponse = await this.reEncryptionBulkOperationBuilder.ExecuteAsync(response, cancellationToken);

                if (bulkOperationResponse.FailedDocuments.Count > 0)
                {
                    foreach ((JObject, Exception) failedOperation in bulkOperationResponse.FailedDocuments)
                    {
                        CosmosException ex = (CosmosException)failedOperation.Item2;
                        if (ex.StatusCode == HttpStatusCode.NotFound)
                        {
                            JObject metadata = failedOperation.Item1.GetValue(Constants.MetadataPropertyName).ToObject<JObject>();
                            string operationType = metadata.GetValue(Constants.OperationTypePropertyName).ToString();
                            // since we pick up only the last change in set of changes for a document, if the last operation is delete we end up with NotFound, so we ignore it.
                            // return the failure for rest of the operation types.
                            if (!operationType.Equals("delete"))
                            {
                                response = new ResponseMessage(
                                    (HttpStatusCode)207, // MultiStatus
                                    response.RequestMessage,
                                    "ReEncryption Operation failed. Please go through ReEncryptionBulkOperationResponse for details regarding failed operations. ");
                                return (response, bulkOperationResponse, response.ContinuationToken);
                            }
                        }
                        else
                        {
                            response = new ResponseMessage(
                                HttpStatusCode.InternalServerError,
                                response.RequestMessage,
                                "ReEncryption Operation failed. Please go through ReEncryptionBulkOperationResponse for details regarding failed operations. ");
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

            this.changeFeedRequestOptions ??= new ChangeFeedRequestOptions
                {
                    PageSizeHint = PageSize,
                };

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
            else if (this.isFullFidelityChangeFeedSupported)
            {
                if (string.IsNullOrEmpty(continuationToken))
                {
                    throw new ArgumentException("SetChangeFeedIterator requires a valid continuation token for getting FullFidelity change feed. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
                }

                feedIterator = this.sourceContainer
                    .GetChangeFeedStreamIterator(
                    ChangeFeedStartFrom.ContinuationToken(continuationToken),
                    ChangeFeedMode.AllVersionsAndDeletes,
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

        private async Task<(string continuationToken, string fullFidelityStartLsn)> ParseContinuationTokenAsync(string continuationToken)
        {
            if (string.IsNullOrEmpty(continuationToken))
            {
                string fullFidelityStartLSNString = await this.GetCurrentLargestCommittedLsnAsync();
                return (continuationToken: null, fullFidelityStartLsn: fullFidelityStartLSNString);
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