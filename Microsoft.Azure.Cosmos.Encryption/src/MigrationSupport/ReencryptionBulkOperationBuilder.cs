// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal class ReencryptionBulkOperationBuilder
    {
        private readonly Container container;
        private readonly string partitionKey;

        public ReencryptionBulkOperationBuilder(
            Container container,
            string partitionKey)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.partitionKey = string.IsNullOrEmpty(partitionKey) ? throw new ArgumentNullException(nameof(partitionKey)) : partitionKey.Substring(1);
        }

        /// <summary>
        /// Builds a bulk operation request and excutes the operation.
        /// </summary>
        /// <param name="response"> Response containing documents read from changefeed. </param>
        /// <param name="cancellationToken"> cancellation token. </param>
        /// <returns> ReencryptionBulkOperationResponse. </returns>
        public async Task<ReencryptionBulkOperationResponse<JObject>> ExecuteAsync(ResponseMessage response, CancellationToken cancellationToken)
        {
            Dictionary<string, List<JObject>> changeFeedChangesBatcher = this.PopulateChangeFeedChanges(response);

            if (!changeFeedChangesBatcher.Any())
            {
                throw new InvalidOperationException("PopulateChangeFeedChanges returned empty list of changes. ");
            }

            ReencryptionBulkOperationResponse<JObject> bulkOperationResponse = null;
            List<JObject> bulkOperationList = this.GetChangesForBulkOperations(changeFeedChangesBatcher);

            if (bulkOperationList.Count > 0)
            {
                ReencryptionBulkOperations<JObject> bulkOperations = new ReencryptionBulkOperations<JObject>(bulkOperationList.Count);

                foreach (JObject document in bulkOperationList)
                {
                    JObject metadata = document.GetValue("_metadata").ToObject<JObject>();
                    string operationType = metadata.GetValue("operationType").ToString();
                    if (operationType.Equals("delete"))
                    {
                        JObject preimage = metadata.GetValue("previousImage").ToObject<JObject>();

                        if (preimage == null)
                        {
                            throw new InvalidOperationException("Missing previous image for document with delete operation type. ");
                        }

                        string id = preimage.GetValue("id").ToString();
                        string pkvalue = preimage.GetValue(this.partitionKey).ToString();

                        bulkOperations.Tasks.Add(this.container.DeleteItemAsync<JObject>(
                           id,
                           new PartitionKey(pkvalue),
                           cancellationToken: cancellationToken).CaptureReencryptionOperationResponseAsync(document));
                    }
                    else
                    {
                        document.Remove("_metadata");
                        document.Remove("_lsn");
                        bulkOperations.Tasks.Add(this.container.UpsertItemAsync(
                           item: document,
                           new PartitionKey(document.GetValue(this.partitionKey).ToString()),
                           cancellationToken: cancellationToken).CaptureReencryptionOperationResponseAsync(document));
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
        /// Builds an array of list of documents and  hashes them by the document id.
        /// If a document has multiple changes then a list of changes(chain) is made corresponding to that id
        /// which serves as a key.
        /// </summary>
        /// <param name="response">Response containing documents read from changefeed .</param>
        /// <returns> HashTable/Array of list hashed/key by document Id. </returns>
        private Dictionary<string, List<JObject>> PopulateChangeFeedChanges(ResponseMessage response)
        {
            JObject contentJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(response.Content);
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

                JObject metadata = document.GetValue("_metadata").ToObject<JObject>();

                if (metadata == null)
                {
                    throw new InvalidOperationException("Metadata property missing in the document. ");
                }

                string operationType = metadata.GetValue("operationType").ToString();
                if (operationType.Equals("delete"))
                {
                    JObject preimage = metadata.GetValue("previousImage").ToObject<JObject>();
                    if (preimage == null)
                    {
                        throw new InvalidOperationException();
                    }

                    string id = preimage.GetValue("id").ToString();

                    if (changeFeedChangesBatcher.ContainsKey(id))
                    {
                        List<JObject> operationToadd = changeFeedChangesBatcher[id];
                        operationToadd.Add(document);
                        changeFeedChangesBatcher[id] = operationToadd;
                    }
                    else
                    {
                        List<JObject> operationToAdd = new List<JObject>
                        {
                            document,
                        };

                        changeFeedChangesBatcher.Add(id, operationToAdd);
                    }
                }
                else
                {
                    string id = document.GetValue("id").ToString();
                    if (changeFeedChangesBatcher.ContainsKey(id))
                    {
                        List<JObject> operationToadd = changeFeedChangesBatcher[id];
                        operationToadd.Add(document);
                        changeFeedChangesBatcher[id] = operationToadd;
                    }
                    else
                    {
                        List<JObject> operationToAdd = new List<JObject>
                        {
                            document,
                        };

                        changeFeedChangesBatcher.Add(id, operationToAdd);
                    }
                }
            }

            return changeFeedChangesBatcher;
        }
    }
}