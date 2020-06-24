//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionFeedIterator : FeedIterator
    {
        private FeedIterator feedIterator;
        private readonly Encryptor encryptor;
        private readonly Container container;
        Action<DecryptionResult> decryptionResultHandler;
        private string continuationToken;
        private QueryDefinition queryDefinition;
        private string queryText;
        private QueryRequestOptions requestOptions;
        public Dictionary<List<string>, string> ToEncrypt = new Dictionary<List<string>, string>();

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            Dictionary<List<string>, string> toEncrypt,
            Action<DecryptionResult> decryptionResultHandler = null)
        {
            this.feedIterator = feedIterator;
            this.encryptor = encryptor;
            this.ToEncrypt = toEncrypt;
            this.decryptionResultHandler = decryptionResultHandler;
        }

        public EncryptionFeedIterator(
            QueryDefinition queryDefinition,
            Dictionary<List<string>, string> toEncrypt,
            QueryRequestOptions requestOptions,
            Encryptor encryptor,
            Container container,
            Action<DecryptionResult> decryptionResultHandler, String continuationToken = null)
        {
            this.queryDefinition = queryDefinition;
            this.ToEncrypt = toEncrypt;
            this.requestOptions = requestOptions;
            this.encryptor = encryptor;
            this.container = container;
            this.decryptionResultHandler = decryptionResultHandler;
            this.continuationToken = continuationToken;
        }

        public EncryptionFeedIterator(
            string queryText,
            Dictionary<List<string>, string> toEncrypt,
            QueryRequestOptions requestOptions,
            Encryptor encryptor,
            Container container,
            Action<DecryptionResult> decryptionResultHandler, String continuationToken = null)
        {
            this.queryText = queryText;
            this.ToEncrypt = toEncrypt;
            this.requestOptions = requestOptions;
            this.encryptor = encryptor;
            this.container = container;
            this.decryptionResultHandler = decryptionResultHandler;
            this.continuationToken = continuationToken;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            if (this.feedIterator == null)
            {
                if (this.queryDefinition != null)
                {
                    QueryDefinition changed = new QueryDefinition(this.queryDefinition.QueryText);
                    foreach (KeyValuePair<string, SqlParameter> parameters in this.queryDefinition.Parameters)
                    {
                        foreach (List<string> path in this.ToEncrypt.Keys)
                        {
                            if (path.Contains("/" + parameters.Key.Substring(4)))
                            {
                                byte[] plaintext = System.Text.Encoding.UTF8.GetBytes((string)parameters.Value.Value);
                                byte[] cyphertext = await this.encryptor.EncryptAsync(
                                    plaintext,
                                    this.ToEncrypt[path],
                                    CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized);

                                changed.WithParameter(parameters.Key, cyphertext);
                            }
                        }
                    }

                    this.feedIterator = this.container.GetItemQueryStreamIterator(
                                            changed,
                                            this.continuationToken,
                                            this.requestOptions);

                }
                else if (this.queryText != null)
                {
                    QueryDefinition query = new QueryDefinition(this.queryText);// = new QueryDefinition(this.queryText);
                    foreach (List<string> Paths in this.ToEncrypt.Keys)
                    {
                        foreach (string Path in Paths)
                        {
                            if (this.queryText.Contains(Path.Substring(1)))
                            {
                                int startindex = this.queryText.IndexOf(Path.Substring(1) + " = '") + (Path.Substring(1) + " = '").Length;
                                string plainVal = this.queryText.Substring(startindex);
                                int endindex = plainVal.IndexOf("'");
                                if (endindex > 0)
                                {
                                    plainVal = plainVal.Substring(0, endindex);
                                }
                                string newqueryText = this.queryText.Replace("'" + plainVal + "'", "@the" + Path.Substring(1));

                                query = new QueryDefinition(newqueryText);

                                byte[] plaintext = System.Text.Encoding.UTF8.GetBytes(plainVal);
                                byte[] cypher = await this.encryptor.EncryptAsync(
                                    plaintext,
                                    this.ToEncrypt[Paths],
                                    CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized);
                                query.WithParameter("@the" + Path.Substring(1), cypher);

                            }
                        }
                    }
                    this.feedIterator = this.container.GetItemQueryStreamIterator(
                                                    query,
                                                    this.continuationToken,
                                                    this.requestOptions);

                }

            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNext"))
            {
                ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);

                if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
                {
                    Stream decryptedContent = await this.DeserializeAndDecryptResponseAsync(
                        responseMessage.Content,
                        diagnosticsContext,
                        cancellationToken,
                         this.ToEncrypt);

                    return new DecryptedResponseMessage(responseMessage, decryptedContent);
                }

                return responseMessage;
            }
        }


        private async Task<Stream> DeserializeAndDecryptResponseAsync(
            Stream content,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken,
             Dictionary<List<string>, string> toEncrypt)
        {
            JObject contentJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(content);
            JArray result = new JArray();

            if (!(contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents))
            {
                throw new InvalidOperationException("Feed response Body Contract was violated. Feed response did not have an array of Documents");
            }

            foreach (JToken value in documents)
            {
                if (!(value is JObject document))
                {
                    result.Add(value);
                    continue;
                }

                try
                {
                    JObject decryptedDocument;
                    if (this.ToEncrypt != null)
                    {
                        decryptedDocument = await EncryptionProcessor.DecryptAsync(
                            document,
                            this.encryptor,
                            diagnosticsContext,
                            cancellationToken, this.ToEncrypt);
                    }
                    else
                    {
                        decryptedDocument = await EncryptionProcessor.DecryptAsync(
                           document,
                           this.encryptor,
                           diagnosticsContext,
                           cancellationToken);
                    }

                    result.Add(decryptedDocument);
                }
                catch (Exception exception)
                {
                    if (this.decryptionResultHandler == null)
                    {
                        throw;
                    }

                    result.Add(document);

                    MemoryStream memoryStream = EncryptionProcessor.BaseSerializer.ToStream(document);
                    Debug.Assert(memoryStream != null);
                    bool wasBufferReturned = memoryStream.TryGetBuffer(out ArraySegment<byte> encryptedStream);
                    Debug.Assert(wasBufferReturned);

                    this.decryptionResultHandler(
                        DecryptionResult.CreateFailure(
                            encryptedStream,
                            exception));
                }
            }

            JObject decryptedResponse = new JObject();
            foreach (JProperty property in contentJObj.Properties())
            {
                if (property.Name.Equals(Constants.DocumentsResourcePropertyName))
                {
                    decryptedResponse.Add(property.Name, (JToken)result);
                }
                else
                {
                    decryptedResponse.Add(property.Name, property.Value);
                }
            }

            return EncryptionProcessor.BaseSerializer.ToStream(decryptedResponse);
        }
    }
}
