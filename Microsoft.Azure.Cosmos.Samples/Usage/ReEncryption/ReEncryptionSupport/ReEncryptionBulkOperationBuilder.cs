// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Cosmos.Samples.ReEncryption
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class ReEncryptionBulkOperationBuilder
    {
        private readonly Container container;
        private readonly string partitionKey;
        private readonly ReEncryptionJsonSerializer reEncryptionJsonSerializer;

        public ReEncryptionBulkOperationBuilder(
            Container container,
            string partitionKeyPath)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.partitionKey = string.IsNullOrEmpty(partitionKeyPath) ? throw new ArgumentNullException(nameof(partitionKeyPath)) : partitionKeyPath[1..];
            this.reEncryptionJsonSerializer = new ReEncryptionJsonSerializer(
                new JsonSerializerSettings()
                {
                    DateParseHandling = DateParseHandling.None,
                });
        }

        /// <summary>
        /// Builds a bulk operation request and excutes the operation.
        /// </summary>
        /// <param name="response"> Response containing documents read from changefeed. </param>
        /// <param name="cancellationToken"> cancellation token. </param>
        /// <returns> ReEncryptionBulkOperationResponse. </returns>
        public async Task<ReEncryptionBulkOperationResponse<JObject>> ExecuteAsync(ResponseMessage response, CancellationToken cancellationToken)
        {
            Dictionary<string, List<JObject>> changeFeedChangesBatcher = this.PopulateChangeFeedChanges(response);

            if (!changeFeedChangesBatcher.Any())
            {
                throw new InvalidOperationException("PopulateChangeFeedChanges returned empty list of changes. ");
            }

            ReEncryptionBulkOperationResponse<JObject> bulkOperationResponse = null;
            List<JObject> bulkOperationList = this.GetChangesForBulkOperations(changeFeedChangesBatcher);

            if (bulkOperationList.Count > 0)
            {
                ReEncryptionBulkOperations<JObject> bulkOperations = new ReEncryptionBulkOperations<JObject>(bulkOperationList.Count);

                foreach (JObject document in bulkOperationList)
                {
                    JObject metadata = document.GetValue(Constants.MetadataPropertyName).ToObject<JObject>();
                    string operationType = metadata.GetValue("operationType").ToString();
                    if (operationType.Equals("delete"))
                    {
                        JObject previousImage = metadata.GetValue(Constants.PreviousImagePropertyName).ToObject<JObject>();

                        if (previousImage == null)
                        {
                            throw new InvalidOperationException("Missing previous image for document with delete operation type. ");
                        }

                        string id = previousImage.GetValue("id").ToString();
                        string pkvalue = previousImage.GetValue(this.partitionKey).ToString();

                        bulkOperations.Tasks.Add(this.container.DeleteItemAsync<JObject>(
                           id,
                           new PartitionKey(pkvalue),
                           cancellationToken: cancellationToken).CaptureReEncryptionOperationResponseAsync(document));
                    }
                    else
                    {
                        document.Remove(Constants.MetadataPropertyName);
                        document.Remove(Constants.LsnPropertyName);
                        bulkOperations.Tasks.Add(this.container.UpsertItemAsync(
                           item: document,
                           new PartitionKey(document.GetValue(this.partitionKey).ToString()),
                           cancellationToken: cancellationToken).CaptureReEncryptionOperationResponseAsync(document));
                    }
                }

                bulkOperationResponse = await bulkOperations.ExecuteAsync();
            }

            return bulkOperationResponse;
        }

        /// <summary>
        /// Iterates over all the changes for each document. If there are multiple changes for the same document,
        /// only the last change is picked up.
        /// <param name="changeFeedChangesBatcher"> List containing the changes. </param>
        /// </summary>
        /// <returns> List of documents. </returns>
        private List<JObject> GetChangesForBulkOperations(Dictionary<string, List<JObject>> changeFeedChangesBatcher)
        {
            List<JObject> bulkOperationList = new List<JObject>();
            foreach (KeyValuePair<string, List<JObject>> keyValuePairOps in changeFeedChangesBatcher)
            {
                // get the last operation if there are multiple changes corresponding to same doc id.
                if (keyValuePairOps.Value.Count > 1)
                {
                    JObject lastDocument = keyValuePairOps.Value.ElementAt(keyValuePairOps.Value.Count - 1);
                    bulkOperationList.Add(lastDocument);
                }
                else if (keyValuePairOps.Value.Count == 1)
                {
                    bulkOperationList.Add(keyValuePairOps.Value.FirstOrDefault());
                }
            }

            return bulkOperationList;
        }

        /// <summary>
        /// Builds a dictionary of list of documents and  hashes them by the document id.
        /// If a document has multiple changes then a list of changes(chain) is made corresponding to that id
        /// which serves as a key.
        /// </summary>
        /// <param name="response">Response containing documents read from changefeed .</param>
        /// <returns> HashTable/Array of list hashed/key by document Id. </returns>
        private Dictionary<string, List<JObject>> PopulateChangeFeedChanges(ResponseMessage response)
        {
            JObject contentJObj = this.reEncryptionJsonSerializer.FromStream<JObject>(response.Content);
            if (!(contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents))
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Feed response did not have an array of Documents. ");
            }

            Dictionary<string, List<JObject>> changeFeedChangesBatcher = new Dictionary<string, List<JObject>>();
            if (documents.Count == 0)
            {
                return changeFeedChangesBatcher;
            }

            foreach (JToken value in documents)
            {
                if (value is not JObject document)
                {
                    continue;
                }

                JObject metadata = document.GetValue(Constants.MetadataPropertyName).ToObject<JObject>();

                if (metadata == null)
                {
                    throw new InvalidOperationException("Metadata property missing in the document. ");
                }

                string operationType = metadata.GetValue(Constants.OperationTypePropertyName).ToString();
                if (operationType.Equals("delete"))
                {
                    JObject previousImage = metadata.GetValue(Constants.PreviousImagePropertyName).ToObject<JObject>();
                    if (previousImage == null)
                    {
                        throw new InvalidOperationException();
                    }

                    string rid = previousImage.GetValue(Constants.DocumentRidPropertyName).ToString();

                    if (changeFeedChangesBatcher.ContainsKey(rid))
                    {
                        List<JObject> operationToAdd = changeFeedChangesBatcher[rid];
                        operationToAdd.Add(document);
                        changeFeedChangesBatcher[rid] = operationToAdd;
                    }
                    else
                    {
                        List<JObject> operationToAdd = new List<JObject>
                        {
                            document,
                        };

                        changeFeedChangesBatcher.Add(rid, operationToAdd);
                    }
                }
                else
                {
                    string rid = document.GetValue(Constants.DocumentRidPropertyName).ToString();
                    if (changeFeedChangesBatcher.ContainsKey(rid))
                    {
                        List<JObject> operationToAdd = changeFeedChangesBatcher[rid];
                        operationToAdd.Add(document);
                        changeFeedChangesBatcher[rid] = operationToAdd;
                    }
                    else
                    {
                        List<JObject> operationToAdd = new List<JObject>
                        {
                            document,
                        };

                        changeFeedChangesBatcher.Add(rid, operationToAdd);
                    }
                }
            }

            return changeFeedChangesBatcher;
        }
    }
}